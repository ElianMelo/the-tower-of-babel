using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    public class MinMaxSliderPropertyDrawer : PropertyDrawerBase
    {
        protected override float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            return (property.propertyType == SerializedPropertyType.Vector2 || property.propertyType == SerializedPropertyType.Vector2Int)
                ? GetPropertyHeight(property)
                : GetPropertyHeight(property) + GetHelpBoxHeight();
        }

        protected override void OnGUI_Internal(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            MinMaxSliderAttribute minMaxSliderAttribute = (MinMaxSliderAttribute)attribute;

            if (property.propertyType == SerializedPropertyType.Vector2 || property.propertyType == SerializedPropertyType.Vector2Int)
            {
                EditorGUI.BeginProperty(rect, label, property);

                float indentLength = NaughtyEditorGUI.GetIndentLength(rect);
                float labelWidth = EditorGUIUtility.labelWidth + NaughtyEditorGUI.HorizontalSpacing;
                float floatFieldWidth = EditorGUIUtility.fieldWidth;
                float SliderWidth = rect.width - labelWidth - 2.0f * floatFieldWidth;
                float SliderPadding = 5.0f;

                Rect labelRect = new Rect(
                    rect.x,
                    rect.y,
                    labelWidth,
                    rect.height);

                Rect SliderRect = new Rect(
                    rect.x + labelWidth + floatFieldWidth + SliderPadding - indentLength,
                    rect.y,
                    SliderWidth - 2.0f * SliderPadding + indentLength,
                    rect.height);

                Rect minFloatFieldRect = new Rect(
                    rect.x + labelWidth - indentLength,
                    rect.y,
                    floatFieldWidth + indentLength,
                    rect.height);

                Rect maxFloatFieldRect = new Rect(
                    rect.x + labelWidth + floatFieldWidth + SliderWidth - indentLength,
                    rect.y,
                    floatFieldWidth + indentLength,
                    rect.height);

                // Draw the label
                EditorGUI.LabelField(labelRect, label.text);

                // Draw the Slider
                EditorGUI.BeginChangeCheck();

                if (property.propertyType == SerializedPropertyType.Vector2)
                {
                    Vector2 SliderValue = property.vector2Value;
                    EditorGUI.MinMaxSlider(SliderRect, ref SliderValue.x, ref SliderValue.y, minMaxSliderAttribute.MinValue, minMaxSliderAttribute.MaxValue);

                    SliderValue.x = EditorGUI.FloatField(minFloatFieldRect, SliderValue.x);
                    SliderValue.x = Mathf.Clamp(SliderValue.x, minMaxSliderAttribute.MinValue, Mathf.Min(minMaxSliderAttribute.MaxValue, SliderValue.y));

                    SliderValue.y = EditorGUI.FloatField(maxFloatFieldRect, SliderValue.y);
                    SliderValue.y = Mathf.Clamp(SliderValue.y, Mathf.Max(minMaxSliderAttribute.MinValue, SliderValue.x), minMaxSliderAttribute.MaxValue);

                    if (EditorGUI.EndChangeCheck())
                    {
                        property.vector2Value = SliderValue;
                    }
                }
                else if (property.propertyType == SerializedPropertyType.Vector2Int)
                {
                    Vector2Int SliderValue = property.vector2IntValue;
                    float xValue = SliderValue.x;
                    float yValue = SliderValue.y;
                    EditorGUI.MinMaxSlider(SliderRect, ref xValue, ref yValue, minMaxSliderAttribute.MinValue, minMaxSliderAttribute.MaxValue);

                    SliderValue.x = EditorGUI.IntField(minFloatFieldRect, (int)xValue);
                    SliderValue.x = (int)Mathf.Clamp(SliderValue.x, minMaxSliderAttribute.MinValue, Mathf.Min(minMaxSliderAttribute.MaxValue, SliderValue.y));

                    SliderValue.y = EditorGUI.IntField(maxFloatFieldRect, (int)yValue);
                    SliderValue.y = (int)Mathf.Clamp(SliderValue.y, Mathf.Max(minMaxSliderAttribute.MinValue, SliderValue.x), minMaxSliderAttribute.MaxValue);

                    if (EditorGUI.EndChangeCheck())
                    {
                        property.vector2IntValue = SliderValue;
                    }
                }

                EditorGUI.EndProperty();
            }
            else
            {
                string message = minMaxSliderAttribute.GetType().Name + " can be used only on Vector2 or Vector2Int fields";
                DrawDefaultPropertyAndHelpBox(rect, property, message, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }
    }
}