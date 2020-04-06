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
        int debugMode = 0;

        protected override void OnSubModuleLoad()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                debugMode = 1;
            }
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



        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            setPresence(loader.INSTANCE.inCampaign);
            /*
            client.SetPresence(new RichPresence()
            {
                Details = (loader.INSTANCE.inCampaign),
                State = debugMode == 1 ? "In Debug Mode" : "",
                Timestamps = Timestamps.Now,
                Assets = new Assets()
                {
                    LargeImageKey = "bannerlord",
                    LargeImageText = loader.INSTANCE.inCampaign,
                }
            });*/
            isPlayerFirst = true;
        }
        
        
        protected override void OnSubModuleUnloaded()
        {
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
            setPresence(loader.INSTANCE.Loading);

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
                        setPresence(loader.INSTANCE.inMenu);
                    }
                    else
                    {
                        if (!GameStateManager.Current.ActiveState.IsMenuState)
                        {
                            inMenuFirst = true;
                        }
                        inCampaign();
                    }
                }
                else
                {
                }
        }

        private void inCampaign()
        {
            if (Agent.Main != null && isPlayerFirst) // IN INSTANCE
            {
                if (Agent.Main.Name != null && !Agent.Main.Name.Equals(""))
                    agentName = Agent.Main.Name;
                isPlayerFirst = false;
                inMenuFirst = true;
                inInstance();
            }
            else if (Agent.Main == null && !isPlayerFirst) // IN CAMPAIGN
            {

                isPlayerFirst = true;
                inMenuFirst = true;
                if (agentName != null)
                    setPresence(loader.INSTANCE.inCampaignAsPlayer);
                else
                    setPresence(loader.INSTANCE.inCampaign);
                inMenuFirst = true;
                isPlayerFirst = true;
            }
        }

        private void inInstance()
        {
            if(Mission.Current != null)
            {
                String playerName = /*debugMode == 1 ? "In Debug Mode" :*/ Mission.Current.MainAgent.Name;
                RichPresence presence = client.CurrentPresence;
                initMission(Mission.Current);
            }
            //setPresence(loader.INSTANCE.inInstanceAsPlayer);
        }

        public void initMission(Mission mission, String playerName= "")
        {
            int ally = 0;
            int enemies = 0;
            String conversation = "";
            float distanceWithMainAgent = 999999;
            foreach(Agent agent in mission.AllAgents)
            {
                if (agent.IsEnemyOf(mission.MainAgent) && agent.IsHuman)
                    enemies++;
                else if (agent.IsHuman && agent.IsFriendOf(mission.MainAgent))
                    ally++;
                else if (agent.IsHuman) { }
                
                if(agent.IsAIControlled && agent.IsHuman && mission.SceneName.ToLower().Contains("conversation"))
                {
                    float tmp = agent.GetTrackDistanceToMainAgent();
                    if (tmp < distanceWithMainAgent)
                    {
                        if (11 > tmp && tmp > 7)
                        {
                            distanceWithMainAgent = tmp;
                            conversation = agent.Name;
                        }
                    }
                }
            }
            // Mission CombatType has cool features.
            if (mission.MainAgent != null && mission.MainAgent.Name != null)
                playerName = mission.MainAgent.Name;
            //if (mission.Mode == MissionMode.Conversation) { setPresence("In Conversation", playerName); }
            if (mission.Mode == MissionMode.Duel) { setPresence("In Duel", playerName); }
            else if (mission.Mode == MissionMode.Tournament) { setPresence("In Tournament against " + enemies + " enemies", playerName); }
            else if (mission.CombatType == Mission.MissionCombatType.ArenaCombat) { setPresence("Fighting in Arena against " + enemies + " enemies", playerName); }
            else if (mission.Mode == MissionMode.Barter) { setPresence("Bartering", playerName); }
            else if (mission.Mode == MissionMode.Battle) { setPresence("In battle against " + enemies + " enemies", playerName); }
            else if (mission.SceneName.ToLower().Contains("conversation"))
            { // Conversation
                String with = "";
                if (!conversation.Trim().Equals(""))
                {
                    with = " with " + conversation;
                }
                setPresence("In Conversation" + with, playerName);
            }
            else if (mission.SceneName.ToLower().Contains("battle_terrain")) { setPresence("In battle against " + enemies + " enemies", playerName); }// battle
            // Checking scene
            else if (mission.SceneName.ToLower().Contains("arena")) { setPresence("In Arena", playerName); }// arena
            else if (mission.SceneName.ToLower().Contains("tavern")) { setPresence("In Tavern", playerName); }// tavern
            else if (mission.SceneName.ToLower().Contains("training_field")) { setPresence("At Training Field", playerName); }// training_field
            else if (mission.SceneName.ToLower().Contains("town"))
            {  // Town
                if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's Town", playerName); }// empire 
                if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's Town", playerName); }// khuzait 
                if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's Town", playerName); }// sturgia 
                if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's Town", playerName); }// vlandia 
                if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's Town", playerName); }// battania 
                if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's Town", playerName); }// aseria 
            }
            else if (mission.SceneName.ToLower().Contains("village"))
            { // Village
                if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's Village", playerName); }// empire 
                if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's Village", playerName); }// khuzait 
                if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's Village", playerName); }// sturgia 
                if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's Village", playerName); }// vlandia 
                if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's Village", playerName); }// battania 
                if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's Village", playerName); }// aseria 
            }
            else if (mission.SceneName.ToLower().Contains("dungeon"))
            { // Dungeon
                if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's Dungeon", playerName); }// empire 
                if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's Dungeon", playerName); }// khuzait 
                if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's Dungeon", playerName); }// sturgia 
                if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's Dungeon", playerName); }// vlandia 
                if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's Dungeon", playerName); }// battania 
                if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's Dungeon", playerName); }// aseria 
            }
            else if (mission.SceneName.ToLower().Contains("city"))
            { // City
                if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's City", playerName); }// empire 
                if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's City", playerName); }// khuzait 
                if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's City", playerName); }// sturgia 
                if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's City", playerName); }// vlandia 
                if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's City", playerName); }// battania 
                if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's City", playerName); }// aseria 
            }
            else if (mission.SceneName.ToLower().Contains("castle"))
            { // Castle
                if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's Castle", playerName); }// empire 
                if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's Castle", playerName); }// khuzait 
                if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's Castle", playerName); }// sturgia 
                if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's Castle", playerName); }// vlandia 
                if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's Castle", playerName); }// battania 
                if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's Castle", playerName); }// aseria 
            }
            else if (mission.SceneName.ToLower().Contains("hideout"))
            { // hideout
                if (mission.SceneName.ToLower().Contains("steppe")) { setPresence("Raiding Steppe Hideout", playerName); }// steppe 
                if (mission.SceneName.ToLower().Contains("mountain")) { setPresence("Raiding Mountain Hideout", playerName); }// mountain 
                if (mission.SceneName.ToLower().Contains("forest")) { setPresence("Raiding Forest Hideout", playerName); }// forest 
                if (mission.SceneName.ToLower().Contains("desert")) { setPresence("Raiding Desert Hideout", playerName); }// desert 
            }



            /*MessageBox.Show(" Scene Name: " + mission.SceneName + "\n Field Battle: " + mission.IsFieldBattle + "\n Character Screen: " + mission.IsCharacterWindowAccessAllowed + 
"\n Mission Mode: " + mission.Mode + "\n Enemy team leader name:" + mission.PlayerEnemyTeam.Leader.Name + "\n Active Agent Enemy Team:" + mission.PlayerEnemyTeam.ActiveAgents.Count);*/
        }

        public override void OnMissionBehaviourInitialize(Mission mission)
        {
            base.OnMissionBehaviourInitialize(mission);
            initMission(mission);
        }

        private void setPresence(String details, String playerName)
        {
            if (client.IsInitialized)
            {
                if (playerName != null && !playerName.Equals("") && details.Contains("&p"))
                    details = Regex.Replace(details, "&p", playerName);
                client.SetPresence(new RichPresence()
                {
                    Details = details,
                    State = !playerName.Trim().Equals("") ? "As " + playerName : "",
                    Timestamps = Timestamps.Now,
                    Assets = new Assets()
                    {
                        LargeImageKey = "bannerlord",
                        LargeImageText = details,
                    }
                });
            }
        }

        private void setPresence(String details)
        {
            if (client.IsInitialized)
            {
                if (agentName != null && !agentName.Equals(""))
                    details = Regex.Replace(details, "&p", agentName);
                client.SetPresence(new RichPresence()
                {
                    Details = details,
                    State = debugMode == 1 ? "In Debug Mode" : "",
                    Timestamps = Timestamps.Now,
                    Assets = new Assets()
                    {
                        LargeImageKey = "bannerlord",
                        LargeImageText = details,
                    }
                });
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

            String file = "{\r\n\t\"ConfigInformation\":[\r\n\t\t\"" +
                "&p indicates in game 'playername'.\",\r\n\t\t\"" +
                "Name Index : Game Name - select the way you want to show game name at Discord. " +
                "Type the desired number to 'selectedName' section.\",\r\n\t\t\"0 " +
                ": Mount & Blade II: Bannerlord,\",\r\n\t\t\"1 : Mount&Blade II: Bannerlord,\",\r\n\t\t\"" +
                "2 : M & B II: Bannerlord,\",\r\n\t\t\"" +
                "3 : M&B II: Bannerlord,\",\r\n\t\t\"" +
                "4 : Bannerlord\"\r\n\t],\r\n\t\"selectedName\":\"0\",\r\n\t\"" +
                "inCampaignAsPlayer\":\"In Campaign as &p\",\r\n\t\"inCampaign\":\"In Campaign\",\r\n\t\"Loading\":\"Loading...\"," +
                "\r\n\t\"inMenu\":\"In Menu\",\r\n\t\"inInstance\":\"In Instance\"," +
                "\r\n\t\"inInstanceAsPlayer\":\"In Instance as &p\"" +
                "\r\n}";
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
        public String fightingAgainst;
        public String conversation;
        public String inLocation;
    }
}
