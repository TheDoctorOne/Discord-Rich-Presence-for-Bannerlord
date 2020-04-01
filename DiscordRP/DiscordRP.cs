using DiscordRPC;
using DiscordRPC.Logging;
using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using System.Threading;
using TaleWorlds.Engine.Screens;

namespace DiscordRP
{
    public class DiscordRP : MBSubModuleBase
    {

        static DiscordRpcClient client;

        protected override void OnSubModuleLoad()
        {
            init();
        }

        

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameLoaded(game, gameStarterObject);
            client.SetPresence(new RichPresence()
            {
                Details = "In Campaing",
                Timestamps = Timestamps.Now,
                Assets = new Assets()
                {
                    LargeImageKey = "bannerlord",
                    LargeImageText = "In Campaing",
                }
            });
            isPlayerFirst = true;
        }

        private void init()
        {
            client = new DiscordRpcClient("694900832935739485");

            //Set the logger
            client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

            //Subscribe to events
            client.OnReady += (sender, e) =>
            {
                Console.WriteLine("Received Ready from user {0}", e.User.Username);
            };

            client.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Received Update! {0}", e.Presence);
            };

            //Connect to the RPC
            client.Initialize();

            //Set the rich presence
            //Call this as many times as you want and anywhere in your code.
            client.SetPresence(new RichPresence()
            {
                Details = "Loading...",
                //State = "Loading Screen",
                Timestamps = Timestamps.Now,
                Assets = new Assets()
                {
                    LargeImageKey = "bannerlord",
                    LargeImageText = "Loading Screen",
                }
            });

            inMenuFirst = true;
        }

        private bool inMenuFirst = true;
        private bool isPlayerFirst = true;
        private String agentName = "";

        protected override void OnApplicationTick(float dt)
        {
            if(client.IsInitialized)
                if(GameStateManager.Current != null && GameStateManager.Current.ActiveState != null)
                {
                    if (GameStateManager.Current.ActiveState.IsMenuState && inMenuFirst)
                    {
                        inMenuFirst = false;
                        isPlayerFirst = true;
                        client.SetPresence(new RichPresence()
                        {
                            Details = "In Menu",
                            Timestamps = Timestamps.Now,
                            Assets = new Assets()
                            {
                                LargeImageKey = "bannerlord",
                                LargeImageText = "In Menu",
                            }
                        });
                    }
                    else
                    {
                        if(!GameStateManager.Current.ActiveState.IsMenuState)
                        {
                            inMenuFirst = true;
                        }
                        if(Agent.Main != null && isPlayerFirst)
                        {
                            if(Agent.Main.Name != null && !Agent.Main.Name.Equals(""))
                                agentName = " as " + Agent.Main.Name;
                            isPlayerFirst = false;
                            inMenuFirst = true;
                            client.SetPresence(new RichPresence()
                            {
                                Details = "In Instance" + agentName,
                                Timestamps = Timestamps.Now,
                                Assets = new Assets()
                                {
                                    LargeImageKey = "bannerlord",
                                    LargeImageText = "In Instance" + agentName,
                                }
                            });
                        }
                        else if(Agent.Main == null && !isPlayerFirst)
                        {
                            isPlayerFirst = true;
                            inMenuFirst = true;
                            client.SetPresence(new RichPresence()
                            {
                                Details = "In Campaing" + agentName,
                                Timestamps = Timestamps.Now,
                                Assets = new Assets()
                                {
                                    LargeImageKey = "bannerlord",
                                    LargeImageText = "In Campaing at Open World",
                                }
                            });
                            inMenuFirst = true;
                            isPlayerFirst = true;
                        }
                    }
                }
                else
                {

                }
        }

    }
}
