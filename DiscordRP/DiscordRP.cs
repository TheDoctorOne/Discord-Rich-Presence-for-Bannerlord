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
using TaleWorlds.CampaignSystem;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using TaleWorlds.Engine;
using static TaleWorlds.MountAndBlade.Mission;
using System.Reflection;
using System.Diagnostics;

namespace DiscordRP
{
    public class DiscordRP : MBSubModuleBase
    {
        int armySize = -1;
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
        int latestPresenceKey = 1010101;
        bool canEnterTournament = false;
        private int latestAllyCount = -1;
        private int latestEnemyCount = -1;
        private int latestArmySize = -1;
        private List<TournamentGame> latestTournamentList = null;
        private bool canEnter = true;
        private bool canResetTournament = true;

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



        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            //Campaign campaign = game.GameType as Campaign;
            //setPresence(loader.INSTANCE.inCampaignAsPlayer, "With Army of " + campaign.MainParty.Party.NumberOfAllMembers, campaign.MainParty.LeaderHero.Name.ToString(), 1);
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
            setPresence(loader.INSTANCE.Loading, -1);
            
        }
        
        
        protected override void OnApplicationTick(float dt)
        {
            if(client != null)
                if (client.IsInitialized)
                    if (GameStateManager.Current != null && GameStateManager.Current.ActiveState != null)
                    {
                        if (GameStateManager.Current.ActiveState.IsMenuState) // MAIN MENU
                        {
                            setPresence(loader.INSTANCE.inMenu, 0);
                        
                        }
                        else
                        {
                            inCampaign();
                        }
                    }
                    else
                    {
                    }
                else if (!client.IsInitialized)
                {
                    client.Initialize();
                }
            else
                {
                    init();
                }
        }

        private void inCampaign()
        {
            if (Agent.Main != null) // IN INSTANCE
            {
                inInstance();
                return;
            }
            else if (Mission.Current == null) // open world
            {
                if (Campaign.Current != null)
                {
                    if (Campaign.Current.MainParty != null)
                    {
                        if(Campaign.Current.MainParty.Party != null && Campaign.Current.MainParty.Leader != null)
                            setPresence(loader.INSTANCE.inCampaignAsPlayer, "With Army of " + (((int)Campaign.Current.MainParty.Party.NumberOfAllMembers) - 1), Campaign.Current.MainParty.Leader.Name.ToString(), 4);
                        else 
                                setPresence(loader.INSTANCE.inCampaign, "In Captivity", "", -98);
                        canResetTournament = true;
                        canEnterTournament = false;
                    }
                }
                canEnter = false;
                if (latestTournamentList == null && Campaign.Current != null && Campaign.Current.TournamentManager != null && Town.All != null)
                {
                    //var tm = Campaign.Current.TournamentManager;
                    //latestTournamentList = new List<TournamentGame>((List<TournamentGame>)tm.GetType().GetField("_activeTournaments", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tm));
                    TournamentGame tmp = null;
                    latestTournamentList = new List<TournamentGame>();
                    foreach(Town t in Town.All)
                    {
                        tmp = Campaign.Current.TournamentManager.GetTournamentGame(t);
                        if(tmp != null)
                        {
                            latestTournamentList.Add(tmp);
                        }
                    }
                }
            }
            if(Campaign.Current != null && Mission.Current == null)
            {
                if (Campaign.Current.MainParty != null)
                {
                    if (Campaign.Current.MainParty.Party != null && Campaign.Current.MainParty.Leader != null)
                        setPresence(loader.INSTANCE.inCampaignAsPlayer, "With Army of " + (((int)Campaign.Current.MainParty.Party.NumberOfAllMembers) - 1), Campaign.Current.MainParty.Leader.Name.ToString(), 4);
                    else
                        setPresence(loader.INSTANCE.inCampaign, "In Captivity", "", -98);
                    canResetTournament = true;
                    canEnterTournament = false;
                }
            }
        }

        private void inInstance()
        {
            if(Mission.Current != null)
            {
                initMission(Mission.Current);
            }
            //setPresence(loader.INSTANCE.inInstanceAsPlayer);
        }


        public void initMission(Mission mission, String playerName= "")
        {
            if (Campaign.Current != null)
                playerName = Campaign.Current.MainParty.Leader.ToString();
            else if (mission.MainAgent != null)
                playerName = mission.MainAgent.Name;

            Agent closestAgent = null;
            bool isBattle = false;
            bool inTournament = false;
            bool inDuel = false;
            bool inArenaCombat = false;

            int ally = 0;
            int enemies = 0;
            String conversation = "";
            float distanceWithMainAgent = 999999;
            foreach(Agent agent in mission.AllAgents.Where(t => t.Health > 0))
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
                        closestAgent = agent;
                        if (11 > tmp && tmp > 7)
                        {
                            distanceWithMainAgent = tmp;
                            conversation = agent.Name;
                        }
                    }
                }
            }
            if (ally != latestAllyCount)
            {
                latestAllyCount = ally;
            }
            if (enemies != latestEnemyCount)
            {
                latestEnemyCount = enemies;
            }
            if(Game.Current != null && Game.Current.GameType is CustomGame) //Custom Game Detection
            {
                if(Agent.Main != null && Agent.Main.Health > 0)
                    setPresence("In Custom Battle" + ((enemies > 0) ? " Against" + enemies : ""), "As " + playerName + ((ally > 0) ? "with " + ally + " Men" : ""), playerName, 122);
                else
                    setPresence("In Custom Battle", "Watching", playerName, 122);
                return;
            }
            String currentPlace = "";
            if (Settlement.CurrentSettlement != null) //Thanks to Aeurias, did not notice Settlement in API. 
            {
                currentPlace = Settlement.CurrentSettlement.Name.ToString();
            }
            // Mission CombatType has cool features.
            //if (mission.Mode == MissionMode.Conversation) { setPresence("In Conversation", playerName); }
            if (Campaign.Current != null && Campaign.Current.TournamentManager != null && !canEnterTournament)
            {
                if (Settlement.CurrentSettlement != null)
                    if (Settlement.CurrentSettlement.IsTown)
                    {
                        if(latestTournamentList != null)
                            foreach (TournamentGame tournament in latestTournamentList)
                            {
                                if (tournament != null && tournament.Town.Name.ToString().Trim().Equals(currentPlace.Trim()))
                                {
                                    canEnterTournament = true;
                                    canResetTournament = false;
                                }
                            }
                        TournamentGame tmp = null;
                        latestTournamentList = new List<TournamentGame>();
                        foreach (Town t in Town.All)
                        {
                            tmp = Campaign.Current.TournamentManager.GetTournamentGame(t);
                            if (tmp != null)
                            {
                                latestTournamentList.Add(tmp);
                            }
                        }
                    }
            }
            if (mission.Mode == MissionMode.Duel) { inDuel = true; }
            else if (mission.Mode == MissionMode.Tournament) { inTournament = true; }
            else if (mission.CombatType == Mission.MissionCombatType.ArenaCombat) { inArenaCombat = true; }
            else if (mission.Mode == MissionMode.Barter) {
                String with = "";
                if (!conversation.Trim().Equals(""))
                {
                    with = " with " + conversation;
                }
                setPresence("Bartering" + with, "As " + playerName,playerName, 8);
                return;
            }
            else if (mission.Mode == MissionMode.Battle) { isBattle = true; }
            
            //Scene Checking Begins - aka where the character is
            if (mission.SceneName.ToLower().Contains("conversation"))
            { // Conversation
                String with = "";
                if (!conversation.Trim().Equals(""))
                {
                    with = " with " + conversation;
                }
                setPresence("In Conversation" + with, "As " + playerName, playerName, 95);
            }
            else if (inDuel)
            {
                String details = "In Duel";
                if (closestAgent != null)
                    details += " against " + closestAgent.Name;
                setPresence(details, "As " + playerName, playerName, 99);
            }
            else if (mission.SceneName.ToLower().Contains("battle_terrain"))
            { // battle
                if(enemies > 0)
                    setPresence("In battle against " + enemies + " enemies", "As " + playerName, playerName, 96);
                else
                    setPresence("In battle", "As " + playerName, playerName, 96);
            }
            else if (mission.SceneName.ToLower().Contains("arena"))
            { // arena
                String details = "In Arena ";
                String state = "";
                if (!currentPlace.Equals(""))
                {
                    details += "at " + currentPlace;
                }
                if (!playerName.Equals(""))
                    state = "As " + playerName;
                if (enemies > 0)
                {
                    state = "Against " + enemies+ " as " + playerName;
                }
                
                if(canEnterTournament)
                {
                    details = "In Tournament";
                    state = "Fighting as " + playerName;
                    if (enemies > 0)
                        details += " Against " + enemies;
                    canResetTournament = true;
                }
                setPresence(details, state, playerName, 90);
                return;
            }
            else if (mission.SceneName.ToLower().Contains("tavern"))
            {// tavern
                String details = "In Tavern ";
                String state = "";
                if (!currentPlace.Equals(""))
                {
                    details += "at " + currentPlace;
                }
                state = "As " + playerName;
                setPresence(details, state, playerName, 91);
            }
            else if (mission.SceneName.ToLower().Contains("training_field"))
            {// training_field
                String details = "In Training Field";
                String state = "As " + playerName; ;
                setPresence(details, state, playerName, -69); //Training field is gay if you say otherwise you gay too.
            }
            else if (mission.SceneName.ToLower().Contains("town"))
            {  // Town
                String details = "In Town";
                String state = "";
                if (!currentPlace.Equals(""))
                {
                    details = "In " + currentPlace;
                }
                state = "As " + playerName;
                if(mission.PlayerEnemyTeam != null && mission.DefenderTeam != null && mission.AttackerTeam != null)
                    if (isBattle && mission.PlayerEnemyTeam != null && mission.PlayerEnemyTeam.ActiveAgents.Count < 8 && canEnter)
                        state = "AMBUSHED!";
                    else if (isBattle && (mission.AttackerTeam.IsPlayerAlly || mission.AttackerTeam.IsPlayerTeam))
                    { // Conquering
                        state = "In Siege Battle as " + playerName;
                        canEnter = false;
                    }
                    else if (isBattle && (mission.DefenderTeam.IsPlayerAlly || mission.DefenderTeam.IsPlayerTeam))
                    { // Conquering
                        state = "Defending as " + playerName;
                        canEnter = false;
                    }
                setPresence(details, state, playerName, 89);
                /*if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's Town", playerName); }// empire 
                if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's Town", playerName); }// khuzait 
                if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's Town", playerName); }// sturgia 
                if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's Town", playerName); }// vlandia 
                if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's Town", playerName); }// battania 
                if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's Town", playerName); }// aseria */
            }
            else if (mission.SceneName.ToLower().Contains("village"))
            { // Village
                String details = "In Village";
                String state = "";
                if (!currentPlace.Equals(""))
                {
                    details = "In " + currentPlace;
                }
                state = "As " + playerName;
                if (isBattle && mission.PlayerEnemyTeam != null && mission.PlayerEnemyTeam.ActiveAgents.Count < 8 && canEnter)
                    state = "AMBUSHED!";
                else if (isBattle)
                { // Helping village
                    state = "Saving Village from Bandits as " + playerName;
                    canEnter = false;
                }
                setPresence(details, state, playerName, 89);
                /* if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's Village", playerName); }// empire 
                 if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's Village", playerName); }// khuzait 
                 if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's Village", playerName); }// sturgia 
                 if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's Village", playerName); }// vlandia 
                 if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's Village", playerName); }// battania 
                 if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's Village", playerName); }// aseria */
            }
            else if (mission.SceneName.ToLower().Contains("dungeon"))
            { // Dungeon
                String details = "In Dungeon ";
                String state = "";
                if (!currentPlace.Equals(""))
                {
                    details += "at " + currentPlace;
                }
                state = "As " + playerName;
                setPresence(details, state, playerName, 88);
                setPresence(details, state, playerName, 88);
                /*if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's Dungeon", playerName); }// empire 
                if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's Dungeon", playerName); }// khuzait 
                if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's Dungeon", playerName); }// sturgia 
                if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's Dungeon", playerName); }// vlandia 
                if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's Dungeon", playerName); }// battania 
                if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's Dungeon", playerName); }// aseria */
            }
            else if (mission.SceneName.ToLower().Contains("city"))
            { // City
                String details = "In City";
                String state = "";
                if (!currentPlace.Equals(""))
                {
                    details = "In " + currentPlace;
                }
                state = "As " + playerName;
                if (isBattle && mission.PlayerEnemyTeam != null && mission.PlayerEnemyTeam.ActiveAgents.Count < 8 && canEnter)
                    state = "AMBUSHED!";
                else if (isBattle && (mission.AttackerTeam.IsPlayerAlly || mission.AttackerTeam.IsPlayerTeam))
                { // Conquering
                    state = "In Siege Battle as " + playerName;
                    canEnter = false;
                }
                else if (isBattle && (mission.DefenderTeam.IsPlayerAlly || mission.DefenderTeam.IsPlayerTeam))
                { // Conquering
                    state = "Defending as " + playerName;
                    canEnter = false;
                }
                setPresence(details, state, playerName, 87);
                /* if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's City", playerName); }// empire 
                 if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's City", playerName); }// khuzait 
                 if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's City", playerName); }// sturgia 
                 if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's City", playerName); }// vlandia 
                 if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's City", playerName); }// battania 
                 if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's City", playerName); }// aseria */
            }
            else if (mission.SceneName.ToLower().Contains("castle"))
            { // Castle
                String details = "In Castle";
                String state = "";
                if (!currentPlace.Equals(""))
                {
                    details = "In " + currentPlace;
                }
                state = "As " + playerName;
                if (isBattle && mission.PlayerEnemyTeam != null && mission.PlayerEnemyTeam.ActiveAgents.Count < 8 && canEnter)
                    state = "AMBUSHED!";
                else if (isBattle && (mission.AttackerTeam.IsPlayerAlly || mission.AttackerTeam.IsPlayerTeam))
                { // Conquering
                    state = "In Siege Battle as " + playerName;
                    canEnter = false;
                }
                else if (isBattle && (mission.DefenderTeam.IsPlayerAlly || mission.DefenderTeam.IsPlayerTeam))
                { // Conquering
                    state = "Defending as " + playerName;
                    canEnter = false;
                }
                setPresence(details, state, playerName, 86);
                /*if (mission.SceneName.ToLower().Contains("empire")) { setPresence("At Empire's Castle", playerName); }// empire 
                if (mission.SceneName.ToLower().Contains("khuzait")) { setPresence("At Khuzait's Castle", playerName); }// khuzait 
                if (mission.SceneName.ToLower().Contains("sturgia")) { setPresence("At Sturgia's Castle", playerName); }// sturgia 
                if (mission.SceneName.ToLower().Contains("vlandia")) { setPresence("At Vlandia's Castle", playerName); }// vlandia 
                if (mission.SceneName.ToLower().Contains("battania")) { setPresence("At Battania's Castle", playerName); }// battania 
                if (mission.SceneName.ToLower().Contains("aseria")) { setPresence("At Aseria's Castle", playerName); }// aseria */
            }
            else if (mission.SceneName.ToLower().Contains("hideout") || mission.SceneName.ToLower().Contains("bandit_forest"))
            { // hideout
                String state = "As " + playerName;
                if (latestAllyCount > 0)
                    state += " with " + latestAllyCount + " Men";
                if (mission.SceneName.ToLower().Contains("steppe")) { setPresence("Raiding Steppe Hideout", state, playerName, 85);  }// steppe 
                else if (mission.SceneName.ToLower().Contains("mountain")) { setPresence("Raiding Mountain Hideout", state, playerName, 84);  }// mountain 
                else if (mission.SceneName.ToLower().Contains("forest")) { setPresence("Raiding Forest Hideout", state, playerName, 83);  }// forest 
                else if (mission.SceneName.ToLower().Contains("desert")) { setPresence("Raiding Desert Hideout", state, playerName, 82);  }// desert
            }
            else if (!currentPlace.Trim().Equals(""))
            {
                String details = "At " + currentPlace;
                String state = "As " + playerName;
                setPresence(details, state, playerName, -2);
            }
            else if (PlayerEncounter.IsActive)
            {
                String encountered = PlayerEncounter.EncounteredParty.Leader.Name.ToString();
                setPresence("Encountered " + encountered, loader.INSTANCE.inCampaignAsPlayer, playerName, 102);
                //setPresence(loader.INSTANCE.inCampaignAsPlayer, "With Army of " + (((int)Campaign.Current.MainParty.Party.NumberOfAllMembers) - 1), Campaign.Current.MainParty.Leader.Name.ToString(), 4);
            }
            if(canResetTournament)
                canEnterTournament = false;

            /*MessageBox.Show(" Scene Name: " + mission.SceneName + "\n Field Battle: " + mission.IsFieldBattle + "\n Character Screen: " + mission.IsCharacterWindowAccessAllowed + 
"\n Mission Mode: " + mission.Mode + "\n Enemy team leader name:" + mission.PlayerEnemyTeam.Leader.Name + "\n Active Agent Enemy Team:" + mission.PlayerEnemyTeam.ActiveAgents.Count);*/
        }

        /*public override void OnMissionBehaviourInitialize(Mission mission)
        {
            base.OnMissionBehaviourInitialize(mission);
            initMission(mission);
        }*/

        private void setPresence(String details, String state, String playerName, int presenceKey, bool forceUpdate = false, Timestamps timestamps = null)
        {
            try
            {
                if (presenceKey == latestPresenceKey)
                {
                    /*if(!forceUpdate)
                        return;*/
                }
                if (client.IsInitialized)
                {
                    if (playerName != null && !playerName.Equals("") && details.Contains("&p"))
                        details = Regex.Replace(details, "&p", playerName);
                    if (Campaign.Current != null && Campaign.Current.MainParty != null && Campaign.Current.MainParty.Party != null)
                        details = Regex.Replace(details, "&a", Campaign.Current.MainParty.Party.NumberOfAllMembers.ToString());
                    if (latestEnemyCount != -1)
                        details = Regex.Replace(details, "&e", latestEnemyCount.ToString());
                    else
                        details = Regex.Replace(details, "&e", "");
                    if (client.CurrentPresence.Details != null && client.CurrentPresence.State != null)
                        if (client.CurrentPresence.Details.Trim().Equals(details.Trim()) && client.CurrentPresence.State.Trim().Equals(state.Trim()))
                            return;
                    if (client.CurrentPresence.Details != null && client.CurrentPresence.State != null && client.CurrentPresence.Timestamps != null)
                        if (!forceUpdate
                            && (client.CurrentPresence.Details.Trim().Equals(details.Trim())
                            || client.CurrentPresence.State.Trim().Equals(state.Trim())))
                            timestamps = client.CurrentPresence.Timestamps;

                    latestPresenceKey = presenceKey;

                    client.SetPresence(new RichPresence()
                    {
                        Details = details,
                        State = state,
                        Timestamps = timestamps != null ? timestamps : Timestamps.Now,
                        Assets = new Assets()
                        {
                            LargeImageKey = "bannerlord",
                            LargeImageText = details,
                        }
                    });
                }
            }
            catch(NullReferenceException ex)
            {
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private void setPresence(String details, int presenceKey, bool forceUpdate = false)
        {
            if (client.IsInitialized && presenceKey != latestPresenceKey)
            {
                if (Campaign.Current != null && Campaign.Current.MainParty != null && Campaign.Current.MainParty.Leader != null)
                    details = Regex.Replace(details, "&p", Campaign.Current.MainParty.Leader.Name.ToString());
                else
                    details = Regex.Replace(details, "&p", "");
                if (Campaign.Current != null && Campaign.Current.MainParty != null && Campaign.Current.MainParty.Party != null)
                    details = Regex.Replace(details, "&a", Campaign.Current.MainParty.Party.NumberOfAllMembers.ToString());
                if (latestEnemyCount != -1)
                    details = Regex.Replace(details, "&e", latestEnemyCount.ToString());
                else
                    details = Regex.Replace(details, "&e", "");
                latestPresenceKey = presenceKey;
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
 //       public String fightingAgainst;
 //       public String conversation;
 //      public String inLocation;
    }


}
