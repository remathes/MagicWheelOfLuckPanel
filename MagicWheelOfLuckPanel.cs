using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Steamworks.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Magic Wheel Of Luck Panel", "TechnicalJunky", "1.0.0")]
    [Description("Players get an item every 24hrs from the Wheel Of Luck!")]
    //Credits go to the author of MagicPanel without his code I would not have been able to learn as much as I did
    //most of the work was copied from other magic panel plugins as a base
    //This was just for fun hope someone enjoys it!
    public class MagicWheelOfLuckPanel : RustPlugin
    {
        #region Class Fields
        [PluginReference] private readonly Plugin MagicPanel;

        private PluginConfig _pluginConfig;
        private string _textFormat;
        private string wheelofluck_default_file;
        private string wheelofluck_included_file;
        private string data_path = string.Empty;
        private List<RustItem> wheelofluckitems = new List<RustItem>();
        private Coroutine _updateRoutine;

        private enum UpdateEnum { All = 1, Panel = 2, Image = 3, Text = 4 }
        #endregion

        #region Get everything ready!
        private void Init()
        {
            _textFormat = _pluginConfig.Panel.Text.Text;
            
            foreach (string file in Directory.GetFiles(Directory.GetCurrentDirectory() + @"\oxide\Data\WheelOfLuck", "*.*", SearchOption.TopDirectoryOnly))
            {
                FileInfo fi = new FileInfo(file);
                if (fi.Name == "WheelOfLuck-Included.json")
                {
                    wheelofluck_included_file = fi.FullName;
                }
                if (fi.Name == "WheelOfLuck-Default.json")
                {
                    wheelofluck_default_file = fi.FullName;
                }
            }

            if (File.Exists(wheelofluck_included_file))
            {
                string json_config = File.ReadAllText(wheelofluck_included_file);
                JToken jtoken_root = JToken.Parse(json_config);
                JToken jtoken_rust_items = jtoken_root["RustItems"];
                List<JToken> rust_items = jtoken_rust_items.ToList();
                if (rust_items.Count > 0)
                {
                    Puts("Building Wheel Of Luck Items...");
                    int count = 0;
                    foreach (var item in rust_items)
                    {
                        try
                        {
                            RustItem rust_item = JsonConvert.DeserializeObject<RustItem>(item.ToString());
                            if (rust_item != null)
                            {
                                count++;
                                if (rust_item.IncludeItem)
                                {
                                    if (rust_item.Name == null)
                                    {
                                        rust_item.Name = rust_item.ShortName;
                                    }
                                    wheelofluckitems.Add(rust_item);
                                }
                            }
                        }
                        catch
                        {
                            Puts("Error converting JToken");
                        }
                    }
                    Puts($"Added {wheelofluckitems.Count} total items to the Wheel Of Luck!");
                }
            }
            else
            {
                if (File.Exists(wheelofluck_default_file))
                {
                    string defaults = File.ReadAllText(wheelofluck_default_file);

                    File.WriteAllText(wheelofluck_included_file, defaults);
                }
                else
                {
                    Puts("Default config file missing. Check to see of the default file exists oxide/Data/WheelOfLuck/WheelOfLuck-Default.json");
                    File.WriteAllText(wheelofluck_included_file, "[]");
                }
            }
        }

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
            config.Panel = new Panel
            {
                Image = new PanelImage
                {
                    Enabled = config.Panel?.Image?.Enabled ?? true,
                    Color = config.Panel?.Image?.Color ?? "#FFFFFFFF",
                    Order = config.Panel?.Image?.Order ?? 0,
                    Width = config.Panel?.Image?.Width ?? 0.33f,
                    Url = config.Panel?.Image?.Url ?? "https://i.postimg.cc/Z57ZbkGc/image1.png",
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
                    Text = config.Panel?.Text?.Text ?? "{0}: {1}",
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
            config.HelpUpdateUITime = "Help info for UpdateUITime. This is how often it will update the Magic Panel. By default it is set to every 30 seconds";
            //How often to update the magic panel this will update the magic panel every 30 seconds by default
            //Lets the user know how much time is left until they can get another item from the wheel of luck every 24hrs new reward on login
            config.UpdateUITime = (float)30.0;
            config.HelpRustItems = @"Help info for WheelOfLuck items to win.  In the oxide\data\WheelOfLuck folder is a file called WheelOfLuck-Included.json this is the default file of items they have a chance to win. "
            + "To change what they can win set the IncludeCategory and IncludeItem to true for every item you want to include, also set the ItemMultiplier. So for a weapon it is defaut set at 1, " +
            "if you set it to 5 they would get 5 of that item. I have an external program to build the file which is available " +
            "for free https://github.com/remathes WheelOfLuck-Config-Generator this is a gui app with all the items from Rust you can set the include and how many items to give the player then save the config.";
            return config;
        }

        private void OnServerInitialized()
        {
            RegisterPlayerPanel();
        }

        private void RegisterPlayerPanel()
        {
            if(MagicPanel == null)
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

        private void UnsubscribeAll()
        {
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerDisconnected));
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
                data_path = Path.GetDirectoryName(wheelofluck_default_file);
                string user_last_login_file = $"{data_path}\\{player.userID}.text";
                yield return null;
                if (File.Exists(user_last_login_file))
                {
                    MagicPanel?.Call("UpdatePanel", player, Name, (int)UpdateEnum.Text);
                }
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (wheelofluck_default_file != "")
            {
                data_path = Path.GetDirectoryName(wheelofluck_default_file);
                string user_last_login_file = $"{data_path}\\{player.userID}.text";
                if (!File.Exists(user_last_login_file))
                {
                    File.WriteAllText(user_last_login_file, $"{DateTime.Now},false");
                    SendWheelOfLuckItems(player);
                }
                else
                {
                    string user_last_login = File.ReadAllText(user_last_login_file);
                    var time_till_next_item = DateTime.Parse(user_last_login.Split(',')[0]).AddDays(1) - DateTime.Now;
                    if(time_till_next_item.Hours<=0&&time_till_next_item.Minutes<=0&&time_till_next_item.Seconds<=0)
                    {
                        File.Delete(user_last_login_file);
                        File.WriteAllText(user_last_login_file, $"{DateTime.Now},false");
                        SendWheelOfLuckItems(player);
                    }
                }
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {

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
            string user_last_login_file = $"{data_path}\\{player.userID}.text";
            if (File.Exists(user_last_login_file))
            {
                string did_user_get_item = File.ReadAllText(user_last_login_file);
                Puts($"{player.displayName}:DailyItemDelivered={did_user_get_item.Split(',')[1]}");
                if (did_user_get_item.Split(',')[1] == "false")
                {
                    if (wheelofluckitems.Count > 0)
                    {
                        if (_pluginConfig != null)
                        {
                            if (_pluginConfig.HowManyItemsToGive >= 1)
                            {
                                var random_items = new System.Random();
                                int total_items_to_give = random_items.Next(1, _pluginConfig.HowManyItemsToGive);
                                if (_pluginConfig.WelcomeMessage != null)
                                {
                                    //Welcome Player On Connect
                                    PrintToChat(_pluginConfig.WelcomeMessage.Replace("player", player.displayName));
                                }
                                PrintToChat($"{player.displayName}, we have {wheelofluckitems.Count.ToString()} items the Wheel Of Luck can give away! Good luck!");
                                PrintToChat($"{player.displayName} just won {total_items_to_give} random items!");

                                for (int i = 0; i < total_items_to_give; i++)
                                {
                                    //Random item selected
                                    var random_item = new System.Random();
                                    int random_item_index = random_item.Next(wheelofluckitems.Count);
                                    RustItem item_to_give = wheelofluckitems[random_item_index];
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

                                File.WriteAllText(user_last_login_file, $"{DateTime.Now},true");
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

        #region MagicPanel Hook

        private Hash<string, object> GetPanel(BasePlayer player)
        {
            Panel panel = _pluginConfig.Panel;
            PanelText text = panel.Text;
            
            if (File.Exists(wheelofluck_default_file))
            {
                data_path = Path.GetDirectoryName(wheelofluck_default_file);
                
                string user_last_login_file = $"{data_path}\\{player.userID}.text";
                string user_last_login = string.Empty;
                
                if (File.Exists(user_last_login_file))
                {
                    user_last_login = File.ReadAllText(user_last_login_file);
                    string user_got_item = user_last_login.Split(',')[1];
                    
                    var time_till_next_item = DateTime.Parse(user_last_login.Split(',')[0]).AddDays(1) - DateTime.Now;
                    if (user_got_item == "false" || time_till_next_item.Hours <= 0 && time_till_next_item.Minutes <= 0 && time_till_next_item.Seconds <= 0)
                    {
                        File.WriteAllText(user_last_login_file, $"{DateTime.Now},false");
                        user_last_login = File.ReadAllText(user_last_login_file);
                        time_till_next_item = DateTime.Parse(user_last_login.Split(',')[0]).AddDays(1) - DateTime.Now;
                        PrintToChat($"{player.displayName}, A New Wheel Of Luck item is on its way!");
                        SendWheelOfLuckItems(player);
                    }
                    if (text != null)
                    {
                        text.Text = string.Format(_textFormat,
                        "New item in",
                            $"{time_till_next_item.Hours} Hr {time_till_next_item.Minutes} Min");
                    }
                }
            }
            else
            {
                if (text != null)
                {
                    text.Text = string.Format(_textFormat,
                    "Error",
                        "Unable to get last login time");
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

        public class RustItem
        {
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
        }
        #endregion
    }
}