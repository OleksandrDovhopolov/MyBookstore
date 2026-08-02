using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Book.Sell.Editor
{
    /// <summary>
    /// Validates quest condition references to dialogue ids. Pure: no logs, no dialogs.
    /// </summary>
    public static class DialogueDeliveredConditionReferenceValidator
    {
        public const string QuestsPath = "Assets/Configs/quests.json";
        public const string DialoguesPath = "Assets/Configs/dialogues.json";
        public const string ConditionType = "dialogueDelivered";

        public static DialogueDeliveredConditionReferenceValidationReport Validate(
            string questsPath = QuestsPath,
            string dialoguesPath = DialoguesPath)
        {
            var report = new DialogueDeliveredConditionReferenceValidationReport();

            if (!TryLoad<QuestConfig>(questsPath, report, out var quests)) return report;
            if (!TryLoad<DialogueConfig>(dialoguesPath, report, out var dialogues)) return report;

            var dialogueIds = new HashSet<string>(
                dialogues.Where(d => !string.IsNullOrWhiteSpace(d?.Id)).Select(d => d.Id),
                StringComparer.Ordinal);

            foreach (var quest in quests)
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.Id)) continue;

                ValidateNode(quest.ActivationConditions, dialogueIds, $"quest '{quest.Id}' activationConditions", report);
                ValidateNode(quest.FailConditions, dialogueIds, $"quest '{quest.Id}' failConditions", report);

                foreach (var task in quest.Tasks ?? Array.Empty<QuestTaskConfig>())
                {
                    if (task == null) continue;
                    var prefix = $"quest '{quest.Id}' task {task.Id}";
                    ValidateNode(task.ActivationConditions, dialogueIds, $"{prefix} activationConditions", report);
                    ValidateNode(task.CompletionConditions, dialogueIds, $"{prefix} completionConditions", report);
                }
            }

            return report;
        }

        private static void ValidateNode(
            JObject node,
            HashSet<string> dialogueIds,
            string path,
            DialogueDeliveredConditionReferenceValidationReport report)
        {
            if (node == null || !node.HasValues) return;

            if (node["all"] is JArray all)
            {
                ValidateArray(all, dialogueIds, $"{path}.all", report);
                return;
            }

            if (node["any"] is JArray any)
            {
                ValidateArray(any, dialogueIds, $"{path}.any", report);
                return;
            }

            if (node["not"] is JObject not)
            {
                ValidateNode(not, dialogueIds, $"{path}.not", report);
                return;
            }

            var type = node.Value<string>("type");
            if (!string.Equals(type, ConditionType, StringComparison.OrdinalIgnoreCase)) return;

            report.CheckedReferences++;
            var dialogueId = node.Value<string>("dialogueId");
            if (string.IsNullOrWhiteSpace(dialogueId))
            {
                report.Errors.Add($"{path} has dialogueDelivered without dialogueId.");
                return;
            }

            if (!dialogueIds.Contains(dialogueId))
                report.Errors.Add($"{path} references missing dialogueId '{dialogueId}'.");
        }

        private static void ValidateArray(
            JArray array,
            HashSet<string> dialogueIds,
            string path,
            DialogueDeliveredConditionReferenceValidationReport report)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is JObject child)
                    ValidateNode(child, dialogueIds, $"{path}[{i}]", report);
            }
        }

        private static bool TryLoad<T>(
            string path,
            DialogueDeliveredConditionReferenceValidationReport report,
            out List<T> items)
        {
            items = null;

            if (!File.Exists(path))
            {
                report.Errors.Add($"File not found: {path}");
                return false;
            }

            try
            {
                items = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
                return true;
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                return false;
            }
        }
    }

    public sealed class DialogueDeliveredConditionReferenceValidationReport
    {
        public List<string> Errors { get; } = new();
        public int CheckedReferences { get; set; }
        public bool HasErrors => Errors.Count > 0;
    }
}
