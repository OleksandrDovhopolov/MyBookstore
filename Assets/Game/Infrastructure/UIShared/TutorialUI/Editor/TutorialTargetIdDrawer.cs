using System;
using System.Linq;
using System.Reflection;
using Infrastructure.TutorialUI;
using UnityEditor;
using UnityEngine;

namespace Infrastructure.TutorialUI.Editor
{
    [CustomPropertyDrawer(typeof(TutorialTargetIdAttribute))]
    public sealed class TutorialTargetIdDrawer : PropertyDrawer
    {
        private const string NoneLabel = "(none)";

        private static readonly TargetOption[] Options = BuildOptions();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[TutorialTargetId] can only be used with string fields.");
                return;
            }

            var values = BuildValues(property.stringValue);
            var labels = values.Select(v => new GUIContent(v.Label)).ToArray();
            var selected = property.hasMultipleDifferentValues ? -1 : IndexOf(values, property.stringValue);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.Popup(position, label, selected, labels);
            if (EditorGUI.EndChangeCheck() && next >= 0 && next < values.Length)
                property.stringValue = values[next].Value;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        private static TargetOption[] BuildValues(string currentValue)
        {
            var values = new System.Collections.Generic.List<TargetOption>(Options.Length + 2)
            {
                new TargetOption(NoneLabel, string.Empty)
            };
            values.AddRange(Options);

            if (string.IsNullOrEmpty(currentValue) || values.Any(v => v.Value == currentValue))
                return values.ToArray();

            values.Add(new TargetOption($"<missing> {currentValue}", currentValue));
            return values.ToArray();
        }

        private static int IndexOf(TargetOption[] values, string value)
        {
            for (var i = 0; i < values.Length; i++)
                if (values[i].Value == value)
                    return i;

            return 0;
        }

        private static TargetOption[] BuildOptions()
            => typeof(TutorialTargetIds)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.FieldType == typeof(string) && f.IsLiteral && !f.IsInitOnly)
                .OrderBy(f => f.MetadataToken)
                .Select(f => new TargetOption($"{f.Name}  ({(string)f.GetRawConstantValue()})", (string)f.GetRawConstantValue()))
                .ToArray();

        private readonly struct TargetOption
        {
            public TargetOption(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public string Value { get; }
        }
    }
}
