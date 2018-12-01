﻿using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Power;
using Content.Server.GameObjects.Components.Interactable.Tools;
using Content.Server.Interfaces.GameObjects;
using Content.Server.Placement;
using SS14.Server;
using SS14.Server.Interfaces;
using SS14.Server.Interfaces.Chat;
using SS14.Server.Interfaces.Maps;
using SS14.Server.Interfaces.Player;
using SS14.Server.Player;
using SS14.Shared.Console;
using SS14.Shared.ContentPack;
using SS14.Shared.Enums;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.Map;
using SS14.Shared.Interfaces.Timers;
using SS14.Shared.IoC;
using SS14.Shared.Log;
using SS14.Shared.Map;
using SS14.Shared.Timers;
using SS14.Shared.Interfaces.Timing;
using SS14.Shared.Maths;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Content.Server.GameObjects.Components.Weapon.Ranged.Projectile;
using Content.Server.GameObjects.Components.Projectiles;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Content.Server.GameObjects.Components.Materials;
using Content.Server.GameObjects.Components.Stack;
using Content.Server.GameObjects.Components.Construction;
using Content.Server.GameObjects.Components.Hydroponics;

namespace Content.Server
{
    public class EntryPoint : GameServer
    {
        private IBaseServer _server;
        private IPlayerManager _players;

        private bool _countdownStarted;

        /// <inheritdoc />
        public override void Init()
        {
            base.Init();

            _server = IoCManager.Resolve<IBaseServer>();
            _players = IoCManager.Resolve<IPlayerManager>();

            _server.RunLevelChanged += HandleRunLevelChanged;
            _players.PlayerStatusChanged += HandlePlayerStatusChanged;
            _players.PlayerPrototypeName = "HumanMob_Content";

            var factory = IoCManager.Resolve<IComponentFactory>();

            factory.Register<HandsComponent>();
            factory.RegisterReference<HandsComponent, IHandsComponent>();

            factory.Register<InventoryComponent>();

            factory.Register<StoreableComponent>();
            factory.Register<ItemComponent>();
            factory.RegisterReference<ItemComponent, StoreableComponent>();
            factory.Register<ClothingComponent>();
            factory.RegisterReference<ClothingComponent, ItemComponent>();

            factory.Register<DamageableComponent>();
            factory.Register<DestructibleComponent>();
            factory.Register<TemperatureComponent>();
            factory.Register<ServerDoorComponent>();

            //Power Components
            factory.Register<PowerTransferComponent>();
            factory.Register<PowerProviderComponent>();
            factory.RegisterReference<PowerProviderComponent, PowerDeviceComponent>();
            factory.Register<PowerNodeComponent>();
            factory.Register<PowerStorageComponent>();
            factory.Register<PowerDeviceComponent>();
            factory.Register<PowerGeneratorComponent>();

            //Tools
            factory.Register<MultitoolComponent>();
            factory.Register<WirecutterComponent>();
            factory.Register<WrenchComponent>();
            factory.Register<WelderComponent>();
            factory.Register<ScrewdriverComponent>();
            factory.Register<CrowbarComponent>();

            factory.Register<HitscanWeaponComponent>();
            factory.Register<ProjectileWeaponComponent>();
            factory.Register<ProjectileComponent>();
            factory.Register<MeleeWeaponComponent>();

            factory.Register<ServerStorageComponent>();

            factory.Register<PowerDebugTool>();
            factory.Register<PoweredLightComponent>();
            factory.Register<SmesComponent>();
            factory.Register<ApcComponent>();
            factory.Register<MaterialComponent>();
            factory.Register<StackComponent>();

            factory.Register<ConstructionComponent>();
            factory.Register<ConstructorComponent>();
            factory.RegisterIgnore("ConstructionGhost");

            //Hydro
            factory.Register<PlantComponent>();
            factory.Register<PlantHolderComponent>();
            factory.Register<SeedPacketComponent>();
            factory.Register<HydroponicsTrayComponent>();

            //TEMPORARY Hydro
            factory.Register<WeedkillerComponent>();
            factory.Register<PestkillerComponent>();
            factory.Register<WaterBucketComponent>();
            factory.Register<NutrientsComponent>();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _server.RunLevelChanged -= HandleRunLevelChanged;
                _players.PlayerStatusChanged -= HandlePlayerStatusChanged;
            }

            base.Dispose(disposing);
        }

        private static void HandleRunLevelChanged(object sender, RunLevelChangedEventArgs args)
        {
            switch (args.NewLevel)
            {
                case ServerRunLevel.PreGame:
                    var timing = IoCManager.Resolve<IGameTiming>();
                    var mapLoader = IoCManager.Resolve<IMapLoader>();
                    var mapMan = IoCManager.Resolve<IMapManager>();

                    var newMap = mapMan.CreateMap();
                    var grid = mapLoader.LoadBlueprint(newMap, "Maps/stationstation.yml");

                    IoCManager.Resolve<IPlayerManager>().FallbackSpawnPoint = new GridLocalCoordinates(Vector2.Zero, grid);

                    var startTime = timing.RealTime;
                    var timeSpan = timing.RealTime - startTime;
                    Logger.Info($"Loaded map in {timeSpan.TotalMilliseconds:N2}ms.");

                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Round loaded!");
                    break;
                case ServerRunLevel.Game:
                    IoCManager.Resolve<IPlayerManager>().SendJoinGameToAll();
                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Round started!");
                    break;
                case ServerRunLevel.PostGame:
                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Round over!");
                    break;
            }
        }

        private void HandlePlayerStatusChanged(object sender, SessionStatusEventArgs args)
        {
            switch (args.NewStatus)
            {
                case SessionStatus.Connected:
                    {
                        // timer time must be > tick length
                        Timer.Spawn(250, args.Session.JoinLobby);

                        IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Player joined server!", args.Session.Index);
                    }
                    break;

                case SessionStatus.InLobby:
                    {
                        // auto start game when first player joins
                        if (_server.RunLevel == ServerRunLevel.PreGame && !_countdownStarted)
                        {
                            _countdownStarted = true;
                            Timer.Spawn(2000, () =>
                            {
                                _server.RunLevel = ServerRunLevel.Game;
                                _countdownStarted = false;
                            });
                        }

                        IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Player joined Lobby!", args.Session.Index);
                    }
                    break;

                case SessionStatus.InGame:
                    {
                        //TODO: Check for existing mob and re-attach
                        IoCManager.Resolve<IPlayerManager>().SpawnPlayerMob(args.Session);

                        IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Player joined Game!", args.Session.Index);
                    }
                    break;

                case SessionStatus.Disconnected:
                    {
                        IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Player left!", args.Session.Index);
                    }
                    break;
            }
        }
    }
}
