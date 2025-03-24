using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Magic Wheel Of Luck Panel", "TechnicalJunky", "1.1.1")]
    [Description("Displays if they have a spin for the WheelOfLuck in a MagicPanel. " +
        "Players get an chance to get a new item every 24hrs from the Wheel Of Luck!")]
    //Credits go to the author of MagicPanel without his code I would not have been able to learn as much as I did
    //most of the work was copied from other magic panel plugins as a base
    //This was just for fun hope someone enjoys it!
    public class MagicWheelOfLuckPanel : RustPlugin
    {
        #region Fields
        [PluginReference] private readonly Plugin MagicPanel;
        private PluginConfig _pluginConfig;
        private string _textFormat;
        private readonly DynamicConfigFile wheelofluck_default_file = Interface.Oxide.DataFileSystem.GetDatafile("WheelOfLuck-Default");
        private readonly DynamicConfigFile wheelofluck_included_file = Interface.Oxide.DataFileSystem.GetDatafile("WheelOfLuck-Included");
        private Coroutine _updateRoutine;
        private string[] images;
        private Timer image_change_timer = null;
        private enum UpdateEnum { All = 1, Panel = 2, Image = 3, Text = 4 }
        #endregion

        #region Commands
        [ChatCommand("wheelofluck")]
        private void WheelOfLuck(BasePlayer player, string command, string[] args)
        {
            var wheelOfLuckPlayer = wheelOfLuckPlayerData.playerData.Where(a => a.PlayerId == player.userID).FirstOrDefault();
            if (args.Length == 0)
            {
                player.ChatMessage("Type /wheelofluck check to see the next time you are able to recieve another spin!");
                player.ChatMessage("Type /wheelofluck spin if you have a spin available");
                return;
            }
            if (args.Length == 1)
            {
                if (args[0] == "check")
                {

                    if (wheelOfLuckPlayer != null)
                    {
                        var time_till_next_item = wheelOfLuckPlayer.LastLogin.AddDays(1) - DateTime.Now;
                        player.ChatMessage($"Next reward in {time_till_next_item.Hours} Hr {time_till_next_item.Minutes} Min");
                    }
                    else
                    {
                        player.ChatMessage("Next reward time is missing please contact the server administrator");
                    }
                }
                if (args[0] == "spin")
                {
                    if (wheelOfLuckPlayer != null)
                    {
                        if (wheelOfLuckPlayer.Spins > 0)
                        {
                            SendWheelOfLuckItems(player);
                        }
                        else
                        {
                            player.ChatMessage("Sorry you do not have any more spins available");
                        }
                    }
                }
            }
            else
            {
                player.ChatMessage("Type /wheelofluck check to see the next time you are able to recieve another spin!");
                player.ChatMessage("Type /wheelofluck spin if you have a spin available");
            }
        }
        #endregion

        #region Get everything ready!
        private void Init()
        {
            try
            {
                Puts("Trying to load player data...");
                wheelOfLuckPlayerData = Interface.Oxide.DataFileSystem.ReadObject<WheelOfLuckPlayerData>("WheelOfLuckPlayers");
                Puts("Player data loaded.");
            }
            catch(Exception ex)
            {
                Puts(ex.Message);
                Puts(ex.StackTrace);
            }
            _textFormat = _pluginConfig.Panel.Text.Text;
            try
            {
                Puts($"Trying to load rust item data...");
                rustItemData = Interface.Oxide.DataFileSystem.ReadObject<RustItemData>("WheelOfLuck-Included");
                Puts($"Rust item data loaded.");
            }
            catch(Exception ex)
            {
                Puts(ex.Message);
                Puts(ex.StackTrace);
            }
            if (rustItemData !=null)
            {
                Puts($"WheelOfLuck contains {rustItemData.rustItems.Where(a=>a.IncludeItem == true).Count()} items to give away!");
            }
            else
            {
                Puts("Config file missing. Check to see of the default file exists oxide/Data/WheelOfLuck-Included.json");
            }
        }
        private void OnServerInitialized()
        {
            RegisterPlayerPanel();
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            PrintToChat(_pluginConfig.WelcomeMessage.Replace("player", player.displayName));
        }
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {

        }
        private void UnsubscribeAll()
        {
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerDisconnected));
        }
        private void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(UpdatePlayerTime);
            if (_updateRoutine != null)
            {
                InvokeHandler.Instance.StopCoroutine(_updateRoutine);
            }
        }
        #endregion
        
        #region Functions
        private void SendWheelOfLuckItems(BasePlayer player)
        {
            var wheelOfLuckPlayer = wheelOfLuckPlayerData.playerData.Where(a => a.PlayerId == player.userID).FirstOrDefault();
            if (rustItemData.rustItems.Where(a=>a.IncludeItem).Count() > 0)
            {
                if (_pluginConfig != null)
                {
                    if (_pluginConfig.HowManyItemsToGive >= 1)
                    {
                        var random_items = new System.Random();
                        int total_items_to_give = random_items.Next(1, _pluginConfig.HowManyItemsToGive);
                        PrintToChat($"{player.displayName}, we have {rustItemData.rustItems.Where(a => a.IncludeItem).Count()} items the Wheel Of Luck can give away! Good luck!");
                        PrintToChat($"{player.displayName} just won {total_items_to_give} random items!");

                        for (int i = 0; i < total_items_to_give; i++)
                        {
                            //Random item selected
                            var random_item = new System.Random();
                            int random_item_index = random_item.Next(rustItemData.rustItems.Where(a => a.IncludeItem).Count());
                            var temp_list = rustItemData.rustItems.Where(a => a.IncludeItem).ToList();
                            RustItem item_to_give = temp_list[random_item_index];
                            if (item_to_give != null)
                            {
                                Item item = GetWheelOfLuckItem(item_to_give.ShortName, item_to_give.AmountOfItemToGive, 0, item_to_give.Name);
                                if (item != null)
                                {
                                    player.GiveItem(item);
                                    PrintToChat($"{player.displayName} just scored {item_to_give.AmountOfItemToGive} {item_to_give.Name} from the Wheel Of Luck, congrats {player.displayName}!");
                                }
                            }
                        }
                        if (wheelOfLuckPlayer != null)
                        {
                            Puts($"Saving user data:{wheelOfLuckPlayer.PlayerName} Current Spins Left: {wheelOfLuckPlayer.Spins}");
                            Puts($"Saving user data:{wheelOfLuckPlayer.PlayerName} Updated Spins Left: {wheelOfLuckPlayer.Spins -1}");
                            wheelOfLuckPlayer.Spins = wheelOfLuckPlayer.Spins - 1;
                            try
                            {
                                Interface.Oxide.DataFileSystem.WriteObject("WheelOfLuckPlayers", wheelOfLuckPlayerData);
                                MagicPanel?.Call("UpdatePanel", player, Name, (int)UpdateEnum.Image);
                            }
                            catch (Exception ex)
                            {
                                Puts(ex.Message);
                                Puts(ex.StackTrace);
                            }
                        }
                    }
                }
            }
        }

        Item GetWheelOfLuckItem(string shortname, int amount, ulong skin, string displayName)
        {
            var item = ItemManager.CreateByName(shortname, amount, skin);
            item.name = displayName;
            return item;
        }
        #endregion

        #region Save/Load
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            string path = $"{Manager.ConfigPath}/MagicPanel/{Name}.json";
            DynamicConfigFile newConfig = new DynamicConfigFile(path);
            if (!newConfig.Exists())
            {
                LoadDefaultConfig();
                newConfig.Save();
            }
            try
            {
                newConfig.Load();
            }
            catch (Exception ex)
            {
                RaiseError("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
                return;
            }

            newConfig.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(newConfig.ReadObject<PluginConfig>());
            newConfig.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            images = new string[] { "https://i.postimg.cc/Z57ZbkGc/image1.png", "https://i.postimg.cc/SK3SKC7Q/image2.png", "https://i.postimg.cc/T3k2ZZKD/image3.png", "https://i.postimg.cc/rpKycQ4Y/image4.png",
                    "https://i.postimg.cc/cH3stCXR/wheelofluckempty.png"};
            config.Panel = new Panel
            {
                Image = new PanelImage
                {
                    Enabled = config.Panel?.Image?.Enabled ?? true,
                    Color = config.Panel?.Image?.Color ?? "#FFFFFFFF",
                    Order = config.Panel?.Image?.Order ?? 0,
                    Width = config.Panel?.Image?.Width ?? 0.33f,
                    Url = config.Panel?.Image?.Url ?? images[0],
                    Padding = config.Panel?.Image?.Padding ?? new TypePadding(0.05f, 0.0f, 0.1f, 0.1f)
                },
                Text = new PanelText
                {
                    Enabled = config.Panel?.Text?.Enabled ?? true,
                    Color = config.Panel?.Text?.Color ?? "#FFFFFFFF",
                    Order = config.Panel?.Text?.Order ?? 1,
                    Width = config.Panel?.Text?.Width ?? 2.4f,
                    FontSize = config.Panel?.Text?.FontSize ?? 14,
                    Padding = config.Panel?.Text?.Padding ?? new TypePadding(0.05f, 0.05f, 0.05f, 0.05f),
                    TextAnchor = config.Panel?.Text?.TextAnchor ?? TextAnchor.MiddleCenter,
                    Text = config.Panel?.Text?.Text ?? "{0}/{1}",
                }
            };
            config.PanelSettings = new PanelRegistration
            {
                BackgroundColor = config.PanelSettings?.BackgroundColor ?? "#FFF2DF08",
                Dock = config.PanelSettings?.Dock ?? "leftmiddle",
                Order = config.PanelSettings?.Order ?? 1,
                Width = config.PanelSettings?.Width ?? 0.0525f
            };
            config.HelpWelcomeMessage = "Help info for WelcomeMessage. You can add a custom message for when they log in.  If it includes the word \"player\" this will be replaced with the player name. " +
                "So the below example would say \"Welcome to our server TechnicalJunky!\"";
            config.WelcomeMessage = "Welcome to our server player!";
            config.HelpHowManyItemsToGive = "Help info for HowManyItemsToGive. This is how many items the player can win. When they log in it will random generate this based on this setting. " +
                "The default is set to 5 so they have a 1 out of 5 chance to win 1,2,3,4 or 5 items";
            //How many items you want to give the player a chance to win
            config.HowManyItemsToGive = 5;
            config.HelpUpdateUITime = "Help info for UpdateUITime. This is how often it will update the Magic Panel. By default it is set to every 5 minutes (300 seconds)";
            //How often to update the magic panel this will update the magic panel every 5 minutes by default
            //Lets the user know they have a reward
            config.UpdateUITime = (float)300.0;
            config.HelpRustItems = @"Help info for WheelOfLuck RustItems.  In the oxide\data folder is a file called WheelOfLuck-Included.json this is the default file of items they have a chance to win. "
            + "To change what they can win set the IncludeCategory and IncludeItem to true for every item you want to include, also set the ItemMultiplier. So for a weapon it is defaut set at 1, " +
            "if you set it to 5 they would get 5 of that item. I have an external program to build the file which is available " +
            "for free https://github.com/remathes/MagicWheelOfLuckPanel WheelOfLuck-Config-Generator this is a gui app with all the items from Rust you can set the include and how many items to give the player then save the config." +
            @" Copy the file it made to oxide\data";
            config.HelpNotifyPlayer = "Help info for NotifyPlayer. When set to false they will not recieve a message when the panel gets updated (every 5 minutes by default)";
            config.NotifyPlayer = true;
            config.HelpMaxSpinsAllowed = "Help info for MaxSpinsAllowed. This is how many spins the player can store before they stop getting new spins.";
            config.MaxSpinsAllowed = 5;
            return config;
        }
        #endregion

        #region MagicPanel Hook
        private void RegisterPlayerPanel()
        {
            if (MagicPanel == null)
            {
                PrintError("Missing plugin dependency MagicPanel: https://umod.org/plugins/magic-panel");
                UnsubscribeAll();
                return;
            }
            InvokeHandler.Instance.InvokeRepeating(UpdatePlayerTime, Random.Range(0, _pluginConfig.UpdateUITime), _pluginConfig.UpdateUITime);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            MagicPanel?.Call("RegisterPlayerPanel", this, Name, JsonConvert.SerializeObject(_pluginConfig.PanelSettings), nameof(GetPanel));
        }
        private void UpdatePlayerTime()
        {
            _updateRoutine = InvokeHandler.Instance.StartCoroutine(HandleUpdatePlayerTime());
        }
        private IEnumerator HandleUpdatePlayerTime()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                yield return null;
                var wheelOfLuckPlayer = wheelOfLuckPlayerData.playerData.Where(a => a.PlayerId == player.userID).FirstOrDefault();
                if (wheelOfLuckPlayer != null)
                {
                    if (wheelOfLuckPlayer.Spins > 0)
                    {
                        if (_pluginConfig.NotifyPlayer)
                        {
                            PrintToChat($"{player.displayName}, You have a spin available. To use it type /wheelofluck spin. We have {rustItemData.rustItems.Where(a => a.IncludeItem).Count()} items to give away!");
                        }
                        image_change_timer = timer.Every(3f, () =>
                        {

                            MagicPanel?.Call("UpdatePanel", player, Name, (int)UpdateEnum.Image);

                        });
                    }
                    else
                    {
                        if (image_change_timer != null)
                        {
                            Puts("Destroying timer");
                            image_change_timer.Destroy();
                        }
                        MagicPanel?.Call("UpdatePanel", player, Name, (int)UpdateEnum.Image);
                    }
                }
                else
                {
                    try
                    {
                        Puts($"Trying to add player data:{player.displayName}");
                        wheelOfLuckPlayerData.playerData.Add(new WheelOfLuckPlayer() { LastLogin = DateTime.Now, PlayerId = player.userID, PlayerName = player.displayName, Spins = 1 });
                        Puts("Player was added.");
                    }
                    catch(Exception ex)
                    {
                        Puts(ex.Message);
                        Puts(ex.StackTrace);
                    }
                    try
                    {
                        Puts("Saving player data");
                        Interface.Oxide.DataFileSystem.WriteObject("WheelOfLuckPlayers", wheelOfLuckPlayerData);
                        Puts("Player data saved");
                    }
                    catch (Exception ex)
                    {
                        Puts(ex.Message);
                        Puts(ex.StackTrace);
                    }
                    if (_pluginConfig.NotifyPlayer)
                    {
                        PrintToChat($"{player.displayName}, You have a spin available. To use it type /wheelofluck spin. We have {rustItemData.rustItems.Where(a => a.IncludeItem).Count()} items to give away!");
                    }
                    image_change_timer = timer.Every(3f, () =>
                    {
                        Puts("Adding timer");
                        MagicPanel?.Call("UpdatePanel", player, Name, (int)UpdateEnum.Image);
                    });
                }
            }
        }
        private Hash<string, object> GetPanel(BasePlayer player)
        {
            Panel panel = _pluginConfig.Panel;
            PanelText text = panel.Text;
            var wheelOfLuckPlayer = wheelOfLuckPlayerData.playerData.Where(a => a.PlayerId == player.userID).FirstOrDefault();
            if (wheelOfLuckPlayer != null)
            {
                var time_till_next_item = wheelOfLuckPlayer.LastLogin.AddDays(1) - DateTime.Now;
                if (time_till_next_item.Hours <= 0 && time_till_next_item.Minutes <= 0 && time_till_next_item.Seconds <= 0)
                {
                    if (_pluginConfig.Panel.Image.Url.Contains(images[0]))
                    {
                        panel.Image.Url = images[1];
                    }
                    else if (_pluginConfig.Panel.Image.Url.Contains(images[1]))
                    {
                        panel.Image.Url = images[2];
                    }
                    else if (_pluginConfig.Panel.Image.Url.Contains(images[2]))
                    {
                        panel.Image.Url = images[3];
                    }
                    else if (_pluginConfig.Panel.Image.Url.Contains(images[3]))
                    {
                        panel.Image.Url = images[0];
                    }
                    if (wheelOfLuckPlayer.Spins < _pluginConfig.MaxSpinsAllowed)
                    {
                        Puts($"Saving user data:{wheelOfLuckPlayer.PlayerName} Current Spins Left: {wheelOfLuckPlayer.Spins}");
                        Puts($"Saving user data:{wheelOfLuckPlayer.PlayerName} Updated Spins Left: {wheelOfLuckPlayer.Spins+1}");
                        wheelOfLuckPlayer.Spins = wheelOfLuckPlayer.Spins + 1;
                        try
                        {
                            Interface.Oxide.DataFileSystem.WriteObject("WheelOfLuckPlayers", wheelOfLuckPlayerData);
                        }
                        catch(Exception ex)
                        {
                            Puts(ex.Message);
                            Puts(ex.StackTrace);
                        }
                    }

                    if (text != null)
                    {
                        text.Text = string.Format(_textFormat,
                        "WheelOfLuck spins " + wheelOfLuckPlayer.Spins+1,
                        _pluginConfig.MaxSpinsAllowed);
                    }
                }
                else
                {
                    if (wheelOfLuckPlayer.Spins == 0)
                    {
                        panel.Image.Url = images[4];
                        text.Text = string.Format(_textFormat,
                        "WheelOfLuck spins " + wheelOfLuckPlayer.Spins,
                        _pluginConfig.MaxSpinsAllowed);
                    }
                    if (wheelOfLuckPlayer.Spins > 0)
                    {
                        if (_pluginConfig.Panel.Image.Url.Contains(images[0]))
                        {
                            panel.Image.Url = images[1];
                        }
                        else if (_pluginConfig.Panel.Image.Url.Contains(images[1]))
                        {
                            panel.Image.Url = images[2];
                        }
                        else if (_pluginConfig.Panel.Image.Url.Contains(images[2]))
                        {
                            panel.Image.Url = images[3];
                        }
                        else if (_pluginConfig.Panel.Image.Url.Contains(images[3]))
                        {
                            panel.Image.Url = images[0];
                        }
                        if (text != null)
                        {
                            text.Text = string.Format(_textFormat,
                            "WheelOfLuck spins " + wheelOfLuckPlayer.Spins,
                            _pluginConfig.MaxSpinsAllowed);
                        }
                    }
                }
            }
            else
            {
                if (_pluginConfig.Panel.Image.Url.Contains(images[0]))
                {
                    panel.Image.Url = images[1];
                }
                else if (_pluginConfig.Panel.Image.Url.Contains(images[1]))
                {
                    panel.Image.Url = images[2];
                }
                else if (_pluginConfig.Panel.Image.Url.Contains(images[2]))
                {
                    panel.Image.Url = images[3];
                }
                else if (_pluginConfig.Panel.Image.Url.Contains(images[3]))
                {
                    panel.Image.Url = images[0];
                }
                try
                {
                    Puts($"Adding new player: {player.displayName}");
                    wheelOfLuckPlayerData.playerData.Add(new WheelOfLuckPlayer() { LastLogin = DateTime.Now, PlayerName = player.displayName, PlayerId = player.userID, Spins = 1 });
                }
                catch(Exception ex)
                {
                    Puts(ex.Message);
                    Puts(ex.StackTrace);
                }
                    
                try
                {
                    Puts($"Saving new player: {player.displayName}");
                    Interface.Oxide.DataFileSystem.WriteObject("WheelOfLuckPlayers", wheelOfLuckPlayerData);
                }
                catch (Exception ex)
                {
                    Puts(ex.Message);
                    Puts(ex.StackTrace);
                }

                //Get new player
                try
                {
                    var new_wheelofluckPlayer = wheelOfLuckPlayerData.playerData.Where(a => a.PlayerId == player.userID).FirstOrDefault();
                    if (new_wheelofluckPlayer != null)
                    {
                        Puts($"Finding new player: {wheelOfLuckPlayer.PlayerName}");
                        text.Text = string.Format(_textFormat,
                        "WheelOfLuck spins " + new_wheelofluckPlayer.Spins,
                        _pluginConfig.MaxSpinsAllowed);
                    }
                }
                catch(Exception ex)
                {
                    Puts(ex.Message);
                    Puts(ex.StackTrace);
                }
            }
            return panel.ToHash();
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Exclude Admins")]
            public bool ExcludeAdmins { get; set; }

            [JsonProperty(PropertyName = "Panel Settings")]
            public PanelRegistration PanelSettings { get; set; }

            [JsonProperty(PropertyName = "Panel Layout")]
            public Panel Panel { get; set; }

            [JsonProperty(PropertyName = "HelpWelcomeMessage")]
            public string HelpWelcomeMessage { get; set; }

            [JsonProperty(PropertyName = "WelcomeMessage")]

            public string WelcomeMessage { get; set; }

            [JsonProperty(PropertyName = "HelpHowManyItemsToGive")]
            public string HelpHowManyItemsToGive { get; set; }

            [JsonProperty(PropertyName = "HowManyItemsToGive")]
            public int HowManyItemsToGive { get; set; }

            [JsonProperty(PropertyName = "HelpUpdateUITime")]
            public string HelpUpdateUITime { get; set; }

            [JsonProperty(PropertyName = "UpdateUITime")]
            public float UpdateUITime { get; set; }

            [JsonProperty(PropertyName = "HelpRustItems")]
            public string HelpRustItems { get; set; }

            [JsonProperty(PropertyName = "HelpNotifyPlayer")]
            public string HelpNotifyPlayer { get; set; }

            [JsonProperty(PropertyName = "NotifyPlayer")]
            public bool NotifyPlayer { get; set; }

            [JsonProperty(PropertyName = "HelpMaxSpinsAllowed")]
            public string HelpMaxSpinsAllowed { get; set; }

            [JsonProperty(PropertyName = "MaxSpinsAllowed")]
            public int MaxSpinsAllowed { get; set; }
        }

        private class PanelRegistration
        {
            public string Dock { get; set; }
            public float Width { get; set; }
            public int Order { get; set; }
            public string BackgroundColor { get; set; }
        }

        private class Panel
        {
            public PanelImage Image { get; set; }
            public PanelText Text { get; set; }

            public Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Image)] = Image.ToHash(),
                    [nameof(Text)] = Text.ToHash()
                };
            }
        }

        private abstract class PanelType
        {
            public bool Enabled { get; set; }
            public string Color { get; set; }
            public int Order { get; set; }
            public float Width { get; set; }
            public TypePadding Padding { get; set; }

            public virtual Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Enabled)] = Enabled,
                    [nameof(Color)] = Color,
                    [nameof(Order)] = Order,
                    [nameof(Width)] = Width,
                    [nameof(Padding)] = Padding.ToHash(),
                };
            }
        }

        private class PanelImage : PanelType
        {
            public string Url { get; set; }

            public override Hash<string, object> ToHash()
            {
                Hash<string, object> hash = base.ToHash();
                hash[nameof(Url)] = Url;
                return hash;
            }
        }

        private class PanelText : PanelType
        {
            public string Text { get; set; }
            public int FontSize { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor TextAnchor { get; set; }

            public override Hash<string, object> ToHash()
            {
                Hash<string, object> hash = base.ToHash();
                hash[nameof(Text)] = Text;
                hash[nameof(FontSize)] = FontSize;
                hash[nameof(TextAnchor)] = TextAnchor;
                return hash;
            }
        }

        private class TypePadding
        {
            public float Left { get; set; }
            public float Right { get; set; }
            public float Top { get; set; }
            public float Bottom { get; set; }

            public TypePadding(float left, float right, float top, float bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Left)] = Left,
                    [nameof(Right)] = Right,
                    [nameof(Top)] = Top,
                    [nameof(Bottom)] = Bottom
                };
            }
        }

        private class RustItemData
        {
            public HashSet<RustItem> rustItems = new HashSet<RustItem>();

            public RustItemData()
            {

            }
        }
        
        private RustItemData rustItemData;
        public class RustItem
        {
            public RustItem()
            {

            }

            public int ItemId { get; set; }
            public bool IncludeItem { get; set; }
            public string ItemImage { get; set; }
            public int ItemMultiplier { get; set; }
            public int AmountOfItemToGive { get; set; }
            public string ShortName { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int Stackable { get; set; }
            public string Category { get; set; }
            public bool IncludeCategory { get; set; }

            public RustItem(int itemId, bool includeItem, string itemImage, int itemMultiplier, int amountOfItemToGive, string shortName, string name, string description, int stackable, string category, bool includeCategory)
            {
                ItemId = itemId;
                IncludeItem = includeItem;
                ItemImage = itemImage;
                ItemMultiplier = itemMultiplier;
                AmountOfItemToGive = amountOfItemToGive;
                ShortName = shortName;
                Name = name;
                Description = description;
                Stackable = stackable;
                Category = category;
                IncludeCategory = includeCategory;
            }
        }

        private class WheelOfLuckPlayerData
        {
            public HashSet<WheelOfLuckPlayer> playerData = new HashSet<WheelOfLuckPlayer>();

            public WheelOfLuckPlayerData()
            {

            }
        }
        public class WheelOfLuckPlayer
        {
            public WheelOfLuckPlayer()
            {

            }

            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public DateTime LastLogin { get; set; }
            public int Spins { get; set; }
            public WheelOfLuckPlayer(ulong playerId, string playerName, DateTime lastLogin, int spins)
            {
                PlayerId = playerId;
                PlayerName = playerName;
                LastLogin = lastLogin;
                Spins = spins;
            }
        }

        private WheelOfLuckPlayerData wheelOfLuckPlayerData;
        #endregion
    }
}
