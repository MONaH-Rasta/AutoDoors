using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Doors", "Wulf/lukespragg/Arainrr", "3.2.0", ResourceId = 1924)]
    [Description("Automatically closes doors behind players after X seconds")]
    public class AutoDoors : RustPlugin
    {
        private bool initialized = false;
        private const string PERMISSION_USE = "autodoors.use";
        private Dictionary<string, string> deployedToName = new Dictionary<string, string>();
        private HashSet<DoorManipulator> doorManipulators = new HashSet<DoorManipulator>();

        private void Init()
        {
            LoadData();
            permission.RegisterPermission(PERMISSION_USE, this);
            cmd.AddChatCommand(configData.chatSettings.command, this, nameof(CmdAutoDoor));
        }

        private void OnServerInitialized()
        {
            UpdateConfig();
            initialized = true;
            foreach (var doorManipulator in BaseNetworkable.serverEntities.OfType<DoorManipulator>())
                OnEntitySpawned(doorManipulator);
        }

        private void UpdateConfig()
        {
            foreach (var itemDefinition in ItemManager.GetItemDefinitions())
            {
                var itemModDeployable = itemDefinition?.GetComponent<ItemModDeployable>();
                if (itemModDeployable == null) continue;
                var door = GameManager.server.FindPrefab(itemModDeployable.entityPrefab.resourcePath)?.GetComponent<Door>();
                if (door == null || string.IsNullOrEmpty(door.ShortPrefabName)) continue;

                if (!configData.doorDisplayNames.ContainsKey(itemDefinition.displayName.english))
                    configData.doorDisplayNames.Add(itemDefinition.displayName.english, itemDefinition.displayName.english);

                if (!deployedToName.ContainsKey(door.ShortPrefabName))
                    deployedToName.Add(door.ShortPrefabName, configData.doorDisplayNames[itemDefinition.displayName.english]);
            }
            SaveConfig();
        }

        private void OnEntitySpawned(DoorManipulator doorManipulator)
        {
            if (!initialized || doorManipulator == null || doorManipulator.OwnerID == 0) return;
            doorManipulators.Add(doorManipulator);
        }

        private void OnEntityKill(BaseCombatEntity baseCombatEntity)
        {
            if (baseCombatEntity == null || baseCombatEntity.OwnerID == 0 || baseCombatEntity.net == null) return;
            if (baseCombatEntity is DoorManipulator)
            {
                var doorManipulator = baseCombatEntity as DoorManipulator;
                doorManipulators.RemoveWhere(x => x == doorManipulator);
            }
            else if (baseCombatEntity is Door)
            {
                var doorID = (baseCombatEntity as Door).net.ID;
                foreach (var entry in storedData.playerData)
                    if (entry.Value.theDoorSettings.ContainsKey(doorID))
                        entry.Value.theDoorSettings.Remove(doorID);
            }
        }

        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), () => SaveData());

        private void Unload() => SaveData();

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            if (door == null || door.net == null || !door.IsOpen() || door.OwnerID == 0) return;
            if (!deployedToName.ContainsKey(door.ShortPrefabName)) return;
            if (configData.usePermissions && !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;
            if (configData.globalSettings.excludeDoorController && HaveDoorController(door)) return;

            float autoCloseTime = 0;
            CreatePlayerData(player.userID);
            var playerDataEntry = storedData.playerData[player.userID];
            if (!playerDataEntry.enabled) return;
            if (playerDataEntry.theDoorSettings.ContainsKey(door.net.ID))
            {
                if (!playerDataEntry.theDoorSettings[door.net.ID].enabled) return;
                autoCloseTime = playerDataEntry.theDoorSettings[door.net.ID].time;
            }
            else if (playerDataEntry.doorTypeSettings.ContainsKey(door.ShortPrefabName))
            {
                if (!playerDataEntry.doorTypeSettings[door.ShortPrefabName].enabled) return;
                autoCloseTime = playerDataEntry.doorTypeSettings[door.ShortPrefabName].time;
            }
            else autoCloseTime = playerDataEntry.time;

            if (autoCloseTime <= 0) return;
            if (Interface.CallHook("CanAutoDoorClose", player, door) != null) return;
            timer.Once(autoCloseTime, () =>
            {
                if (door == null || !door.IsOpen()) return;
                if (configData.globalSettings.cancelOnKill && player.IsDead()) return;
                door.SetFlag(BaseEntity.Flags.Open, false);
                door.SendNetworkUpdateImmediate();
            });
        }

        private bool HaveDoorController(Door door)
        {
            foreach (var doorManipulator in doorManipulators)
                if (doorManipulator != null && doorManipulator.targetDoor == door)
                    return true;
            return false;
        }

        private void CreatePlayerData(ulong playerID)
        {
            if (storedData.playerData.ContainsKey(playerID)) return;
            storedData.playerData.Add(playerID, new StoredData.PlayerDataEntry
            {
                enabled = configData.globalSettings.defaultEnabled,
                time = configData.globalSettings.defaultDelay,
            });
        }

        private Door GetLookingDoor(BasePlayer player)
        {
            RaycastHit raycastHit;
            if (Physics.Raycast(player.eyes.HeadRay(), out raycastHit))
                if (raycastHit.GetEntity() is Door)
                {
                    var door = raycastHit.GetEntity() as Door;
                    if (door.OwnerID != 0 && deployedToName.ContainsKey(door.ShortPrefabName))
                        return door;
                }
            return null;
        }

        #region ChatCommand

        private void CmdAutoDoor(BasePlayer player, string command, string[] args)
        {
            if (configData.usePermissions && !permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            CreatePlayerData(player.userID);
            if (args == null || args.Length == 0)
            {
                storedData.playerData[player.userID].enabled = !storedData.playerData[player.userID].enabled;
                if (storedData.playerData[player.userID].enabled) Print(player, Lang("AutoDoor", player.UserIDString, Lang("Enabled", player.UserIDString)));
                else Print(player, Lang("AutoDoor", player.UserIDString, Lang("Disabled", player.UserIDString)));
                return;
            }
            switch (args[0].ToLower())
            {
                case "a":
                case "all":
                    float time = 0;
                    if (args.Length >= 2 && float.TryParse(args[1], out time))
                    {
                        if (time <= configData.globalSettings.maximumDelay && time >= configData.globalSettings.minimumDelay)
                        {
                            storedData.playerData[player.userID].time = time;
                            foreach (var key in storedData.playerData[player.userID].doorTypeSettings.Keys)
                                storedData.playerData[player.userID].doorTypeSettings[key].time = time;
                            foreach (var key in storedData.playerData[player.userID].theDoorSettings.Keys)
                                storedData.playerData[player.userID].theDoorSettings[key].time = time;
                            Print(player, Lang("AutoDoorDelay", player.UserIDString, time));
                        }
                        else Print(player, Lang("AutoDoorDelayLimit", player.UserIDString, configData.globalSettings.minimumDelay, configData.globalSettings.maximumDelay));
                        return;
                    }
                    break;

                case "s":
                case "single":
                    var door = GetLookingDoor(player);
                    if (door?.net == null)
                    {
                        Print(player, Lang("DoorNoFound", player.UserIDString));
                        return;
                    }
                    if (!storedData.playerData[player.userID].theDoorSettings.ContainsKey(door.net.ID))
                        storedData.playerData[player.userID].theDoorSettings.Add(door.net.ID, new StoredData.DoorSettings { enabled = true, time = configData.globalSettings.defaultDelay });

                    if (args.Length <= 1)
                    {
                        storedData.playerData[player.userID].theDoorSettings[door.net.ID].enabled = !storedData.playerData[player.userID].theDoorSettings[door.net.ID].enabled;
                        if (storedData.playerData[player.userID].theDoorSettings[door.net.ID].enabled) Print(player, Lang("AutoDoorSingle", player.UserIDString, deployedToName[door.ShortPrefabName], Lang("Enabled", player.UserIDString)));
                        else Print(player, Lang("AutoDoorSingle", player.UserIDString, deployedToName[door.ShortPrefabName], Lang("Disabled", player.UserIDString)));
                        return;
                    }
                    else
                    {
                        float time1 = 0;
                        if (float.TryParse(args[1], out time1))
                        {
                            if (time1 <= configData.globalSettings.maximumDelay && time1 >= configData.globalSettings.minimumDelay)
                            {
                                storedData.playerData[player.userID].theDoorSettings[door.net.ID].time = time1;
                                Print(player, Lang("AutoDoorSingleDelay", player.UserIDString, deployedToName[door.ShortPrefabName], time1));
                            }
                            else Print(player, Lang("AutoDoorDelayLimit", player.UserIDString, configData.globalSettings.minimumDelay, configData.globalSettings.maximumDelay));
                            return;
                        }
                    }
                    break;

                case "t":
                case "type":
                    var door1 = GetLookingDoor(player);
                    if (door1?.net == null)
                    {
                        Print(player, Lang("DoorNoFound", player.UserIDString));
                        return;
                    }
                    if (!storedData.playerData[player.userID].doorTypeSettings.ContainsKey(door1.ShortPrefabName))
                        storedData.playerData[player.userID].doorTypeSettings.Add(door1.ShortPrefabName, new StoredData.DoorSettings { enabled = true, time = configData.globalSettings.defaultDelay });

                    if (args.Length <= 1)
                    {
                        storedData.playerData[player.userID].doorTypeSettings[door1.ShortPrefabName].enabled = !storedData.playerData[player.userID].doorTypeSettings[door1.ShortPrefabName].enabled;

                        if (storedData.playerData[player.userID].doorTypeSettings[door1.ShortPrefabName].enabled) Print(player, Lang("AutoDoorType", player.UserIDString, deployedToName[door1.ShortPrefabName], Lang("Enabled", player.UserIDString)));
                        else Print(player, Lang("AutoDoorType", player.UserIDString, deployedToName[door1.ShortPrefabName], Lang("Disabled", player.UserIDString)));
                        return;
                    }
                    else
                    {
                        float time1 = 0;
                        if (float.TryParse(args[1], out time1))
                        {
                            if (time1 <= configData.globalSettings.maximumDelay && time1 >= configData.globalSettings.minimumDelay)
                            {
                                storedData.playerData[player.userID].doorTypeSettings[door1.ShortPrefabName].time = time1;
                                Print(player, Lang("AutoDoorTypeDelay", player.UserIDString, deployedToName[door1.ShortPrefabName], time1));
                            }
                            else Print(player, Lang("AutoDoorDelayLimit", player.UserIDString, configData.globalSettings.minimumDelay, configData.globalSettings.maximumDelay));
                            return;
                        }
                    }
                    break;

                case "h":
                case "help":
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(Lang("AutoDoorSyntax", player.UserIDString, configData.chatSettings.command));
                    stringBuilder.AppendLine(Lang("AutoDoorSyntax1", player.UserIDString, configData.chatSettings.command, configData.globalSettings.minimumDelay, configData.globalSettings.maximumDelay));
                    stringBuilder.AppendLine(Lang("AutoDoorSyntax2", player.UserIDString, configData.chatSettings.command));
                    stringBuilder.AppendLine(Lang("AutoDoorSyntax3", player.UserIDString, configData.chatSettings.command, configData.globalSettings.minimumDelay, configData.globalSettings.maximumDelay));
                    stringBuilder.AppendLine(Lang("AutoDoorSyntax4", player.UserIDString, configData.chatSettings.command));
                    stringBuilder.AppendLine(Lang("AutoDoorSyntax5", player.UserIDString, configData.chatSettings.command, configData.globalSettings.minimumDelay, configData.globalSettings.maximumDelay));
                    Print(player, $"\n{stringBuilder.ToString()}");
                    return;
            }
            Print(player, Lang("SyntaxError", player.UserIDString, configData.chatSettings.command));
        }

        #endregion ChatCommand

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Use permissions")]
            public bool usePermissions = false;

            [JsonProperty(PropertyName = "Global settings")]
            public GlobalSettings globalSettings = new GlobalSettings();

            [JsonProperty(PropertyName = "Chat settings")]
            public ChatSettings chatSettings = new ChatSettings();

            [JsonProperty(PropertyName = "Door display names")]
            public Dictionary<string, string> doorDisplayNames = new Dictionary<string, string>();

            public class GlobalSettings
            {
                [JsonProperty(PropertyName = "Exclude door controller")]
                public bool excludeDoorController = true;

                [JsonProperty(PropertyName = "Cancel on player dead")]
                public bool cancelOnKill = false;

                [JsonProperty(PropertyName = "Default enabled")]
                public bool defaultEnabled = true;

                [JsonProperty(PropertyName = "Default delay")]
                public float defaultDelay = 5f;

                [JsonProperty(PropertyName = "Maximum delay")]
                public float maximumDelay = 10f;

                [JsonProperty(PropertyName = "Minimum delay")]
                public float minimumDelay = 5f;
            }

            public class ChatSettings
            {
                [JsonProperty(PropertyName = "Chat command")]
                public string command = "autodoor";

                [JsonProperty(PropertyName = "Chat prefix")]
                public string prefix = "[AutoDoors]: ";

                [JsonProperty(PropertyName = "Chat prefix color")]
                public string prefixColor = "#00FFFF";

                [JsonProperty(PropertyName = "Chat steamID icon")]
                public ulong steamIDIcon = 0;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(configData);

        #endregion ConfigurationFile

        #region DataFile

        private StoredData storedData;

        private class StoredData
        {
            public Dictionary<ulong, PlayerDataEntry> playerData = new Dictionary<ulong, PlayerDataEntry>();

            public class PlayerDataEntry
            {
                public bool enabled;
                public float time;
                public Dictionary<uint, DoorSettings> theDoorSettings = new Dictionary<uint, DoorSettings>();
                public Dictionary<string, DoorSettings> doorTypeSettings = new Dictionary<string, DoorSettings>();
            }

            public class DoorSettings
            {
                public bool enabled;
                public float time;
            }
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                ClearData();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void ClearData()
        {
            storedData = new StoredData();
            SaveData();
        }

        private void OnNewSave(string filename)
        {
            foreach (var data in storedData.playerData)
                data.Value.theDoorSettings.Clear();
            SaveData();
        }

        #endregion DataFile

        #region LanguageFile

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You do not have permission to use this command",
                ["Enabled"] = "<color=#8ee700>Enabled</color>",
                ["Disabled"] = "<color=#ce422b>Disabled</color>",
                ["AutoDoor"] = "Automatic door closing is now {0}",
                ["AutoDoorDelay"] = "Automatic door closing delay set to {0}s",
                ["DoorNoFound"] = "Please looking at the door or not support the door",
                ["AutoDoorDelayLimit"] = "Automatic door closing delay is between {0}s and {1}s",
                ["AutoDoorSingle"] = "Automatic closing of the {0} is {1}",
                ["AutoDoorSingleDelay"] = "Automatic closing delay of the {0} is {1}s",
                ["AutoDoorType"] = "Automatic closing of {0} door is {1}",
                ["AutoDoorTypeDelay"] = "Automatic closing delay of {0} door is {1}s",
                ["SyntaxError"] = "Syntax error, please enter '<color=#ce422b>/{0} <help | h></color>' to view help",

                ["AutoDoorSyntax"] = "<color=#ce422b>/{0} </color> - Enable/Disable automatic door closing",
                ["AutoDoorSyntax1"] = "<color=#ce422b>/{0} <all | a> <time (seconds)></color> - Set automatic closing delay for all doors, (The time is between {1} and {2})",
                ["AutoDoorSyntax2"] = "<color=#ce422b>/{0} <single | s></color> - Enable/Disable automatic closing of the door you are looking at",
                ["AutoDoorSyntax3"] = "<color=#ce422b>/{0} <single | s> <time (seconds)></color> - Set automatic closing delay for the door you are looking at, (The time is between {1} and {2})",
                ["AutoDoorSyntax4"] = "<color=#ce422b>/{0} <type | t></color> - Enable/disable automatic door closing for the type of door you are looking at",
                ["AutoDoorSyntax5"] = "<color=#ce422b>/{0} <type | t> <time (seconds)></color> - Set automatic closing delay for the type of door you are looking at, (The time is between {1} and {2})",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "您没有权限使用该命令",
                ["Enabled"] = "<color=#8ee700>已启用</color>",
                ["Disabled"] = "<color=#ce422b>已禁用</color>",
                ["AutoDoor"] = "自动关门现在的状态为 {0}",
                ["AutoDoorDelay"] = "自动关门延迟现在设置为 {0}s",
                ["DoorNoFound"] = "请看着门/不支持这种门",
                ["AutoDoorDelayLimit"] = "自动关门延迟应该在 {0}s 和 {1}s 之间",
                ["AutoDoorSingle"] = "这个 {0} 的自动关门状态为 {1}",
                ["AutoDoorSingleDelay"] = "这个 {0} 的自动关门延迟为 {1}s",
                ["AutoDoorType"] = "{0} 的自动关门状态为 {1}",
                ["AutoDoorTypeDelay"] = "{0} 的自动关门延迟为 {1}s",
                ["SyntaxError"] = "语法错误, 输入 '<color=#ce422b>/{0} <help | h></color>' 查看帮助",

                ["AutoDoorSyntax"] = "<color=#ce422b>/{0} </color> - 启用/禁用自动关门",
                ["AutoDoorSyntax1"] = "<color=#ce422b>/{0} <all | a> <时间 (秒)></color> - 为所有门设置自动关门延迟，(时间在 {1} 和 {2} 之间)",
                ["AutoDoorSyntax2"] = "<color=#ce422b>/{0} <single | s></color> - 为您看着的这个门，启用/禁用自动关门",
                ["AutoDoorSyntax3"] = "<color=#ce422b>/{0} <single | s> <时间 (秒)></color> - 为您看着的这个门设置自动关门延迟，(时间在 {1} 和 {2} 之间)",
                ["AutoDoorSyntax4"] = "<color=#ce422b>/{0} <type | t></color> - 为您看着的这种门，启用/禁用自动关门",
                ["AutoDoorSyntax5"] = "<color=#ce422b>/{0} <type | t> <时间 (秒)></color> - 为您看着的这种门设置自动关门延迟，(时间在 {1} 和 {2} 之间)",
            }, this, "zh-CN");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Print(BasePlayer player, string message) => Player.Message(player, message, $"<color={configData.chatSettings.prefixColor}>{configData.chatSettings.prefix}</color>", configData.chatSettings.steamIDIcon);

        #endregion LanguageFile
    }
}