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
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace DiscordRP
{
    public class DiscordRP : MBSubModuleBase
    {

        static DiscordRpcClient client;
        private configLoader loader = new configLoader();
        private String[] clientIDs = { "694900832935739485", "695787558012977184", "695784986086735983", "695786091180982372", "695789819049017385" };
        /*
         * 0 : Mount & Blade II: Bannerlord
         * 1 : Mount&Blade II: Bannerlord
         * 2 : M & B II: Bannerlord
         * 3 : M&B II: Bannerlord
         * 4 : Bannerlord
         */

        protected override void OnSubModuleLoad()
        {
            init();
        }

        public void init()
        {
            try
            {
                init(int.Parse(loader.INSTANCE.selectedName));
            }
            catch (FormatException)
            {
                init(0);
            }
        }



        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameLoaded(game, gameStarterObject);
            client.SetPresence(new RichPresence()
            {
                Details = loader.INSTANCE.inCampaign,
                Timestamps = Timestamps.Now,
                Assets = new Assets()
                {
                    LargeImageKey = "bannerlord",
                    LargeImageText = loader.INSTANCE.inCampaign,
                }
            });
            isPlayerFirst = true;
        }
        
        
        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            client.Dispose();
        }

        private void init(int id)
        {
            if (id >= clientIDs.Length)
                id = 0;
            client = new DiscordRpcClient(clientIDs[id]);

            client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

            client.OnReady += (sender, e) =>
            {
                Console.WriteLine("Received Ready from user {0}", e.User.Username);
            };

            client.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Received Update! {0}", e.Presence);
            };

            client.Initialize();

            client.SetPresence(new RichPresence()
            {
                Details = loader.INSTANCE.Loading,
                Timestamps = Timestamps.Now,
                Assets = new Assets()
                {
                    LargeImageKey = "bannerlord",
                    LargeImageText = loader.INSTANCE.Loading,
                }
            });

            inMenuFirst = true;
        }

        private bool inMenuFirst = true;
        private bool isPlayerFirst = true;
        private String agentName = "";
        
        protected override void OnApplicationTick(float dt)
        {
            if (client.IsInitialized)
                if (GameStateManager.Current != null && GameStateManager.Current.ActiveState != null)
                {
                    if (GameStateManager.Current.ActiveState.IsMenuState && inMenuFirst) // MAIN MENU
                    {
                        inMenuFirst = false;
                        isPlayerFirst = true;
                        client.SetPresence(new RichPresence()
                        {
                            Details = loader.INSTANCE.inMenu,
                            Timestamps = Timestamps.Now,
                            Assets = new Assets()
                            {
                                LargeImageKey = "bannerlord",
                                LargeImageText = loader.INSTANCE.inMenu,
                            }
                        });
                    }
                    else
                    {
                        if (!GameStateManager.Current.ActiveState.IsMenuState)
                        {
                            inMenuFirst = true;
                        }
                        if (Agent.Main != null && isPlayerFirst) // IN INSTANCE
                        {
                            if (Agent.Main.Name != null && !Agent.Main.Name.Equals(""))
                                agentName = Agent.Main.Name;
                            isPlayerFirst = false;
                            inMenuFirst = true;
                            client.SetPresence(new RichPresence()
                            {
                                Details = Regex.Replace(loader.INSTANCE.inInstanceAsPlayer, "&p", agentName),
                                Timestamps = Timestamps.Now,
                                Assets = new Assets()
                                {
                                    LargeImageKey = "bannerlord",
                                    LargeImageText = Regex.Replace(loader.INSTANCE.inInstanceAsPlayer, "&p", agentName),
                                }
                            });
                        }
                        else if (Agent.Main == null && !isPlayerFirst) // IN CAMPAIGN
                        {
                            
                            isPlayerFirst = true;
                            inMenuFirst = true;
                            if (agentName != null)
                                client.SetPresence(new RichPresence()
                                {
                                    Details = Regex.Replace(loader.INSTANCE.inCampaignAsPlayer, "&p", agentName),
                                    Timestamps = Timestamps.Now,
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "bannerlord",
                                        LargeImageText = Regex.Replace(loader.INSTANCE.inCampaignAsPlayer, "&p", agentName),
                                    }
                                });
                            else
                                client.SetPresence(new RichPresence()
                                {
                                    Details = loader.INSTANCE.inCampaign,
                                    Timestamps = Timestamps.Now,
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "bannerlord",
                                        LargeImageText = loader.INSTANCE.inCampaign,
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

    public class configLoader
    {
        public config INSTANCE = new config();
        private String path = "../../Modules/DiscordRP/discordRPConfig.json";
        public configLoader()
        {
            reload();
        }

        public void reload()
        {
            if (File.Exists(path))
            {
                try
                {
                    String file = File.ReadAllText(path);
                    INSTANCE = JsonConvert.DeserializeObject<config>(Regex.Replace(Regex.Replace(file, "\r\n", ""), "\t", ""));
                }
                catch
                {
                    writeJson();
                }
            }
            else
            {
                writeJson();
            }
        }

        public void writeJson()
        {
            String[] info = { "&p indicates in game 'playername'." ,
                "Name Index : Game Name - select the way you want to show game name at Discord. Type the desired number to 'selectedName' section." ,
                "0 : Mount & Blade II: Bannerlord," ,
                "1 : Mount&Blade II: Bannerlord," ,
                "2 : M & B II: Bannerlord," ,
                "3 : M&B II: Bannerlord," ,
                "4 : Bannerlord"
            };
            INSTANCE.ConfigInformation = info;
            INSTANCE.selectedName = "0";
            INSTANCE.inCampaign = "In Campaign";
            INSTANCE.inInstance = "In Instance";
            INSTANCE.inInstanceAsPlayer = "In Instance as &p";
            INSTANCE.inMenu = "In Menu";
            INSTANCE.Loading = "Loading...";
            INSTANCE.inCampaignAsPlayer = "In Campaign as &p";

            String file = "{\r\n\t\"ConfigInformation\":[\r\n\t\t\"&p indicates in game 'playername'.\",\r\n\t\t\"Name Index : Game Name - select the way you want to show game name at Discord. Type the desired number to 'selectedName' section.\",\r\n\t\t\"0 : Mount & Blade II: Bannerlord,\",\r\n\t\t\"1 : Mount&Blade II: Bannerlord,\",\r\n\t\t\"2 : M & B II: Bannerlord,\",\r\n\t\t\"3 : M&B II: Bannerlord,\",\r\n\t\t\"4 : Bannerlord\"\r\n\t],\r\n\t\"selectedName\":\"0\",\r\n\t\"inCampaignAsPlayer\":\"In Campaign as &p\",\r\n\t\"inCampaign\":\"In Campaign\",\r\n\t\"Loading\":\"Loading...\",\r\n\t\"inMenu\":\"In Menu\",\r\n\t\"inInstance\":\"In Instance\",\r\n\t\"inInstanceAsPlayer\":\"In Instance as &p\"\r\n}";
            StreamWriter writer = new StreamWriter(path);
            writer.Write(file);
            writer.Close();
        }
    }

    public class config
    {
        public String[] ConfigInformation;
        public String selectedName;
        public String inCampaignAsPlayer;
        public String inCampaign;
        public String Loading;
        public String inMenu;
        public String inInstance;
        public String inInstanceAsPlayer;
    }
}
