using UnityEngine;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class MaxValuePropertyValidator : PropertyValidatorBase
    {
        public override void ValidateProperty(SerializedProperty property)
        {
            MaxValueAttribute maxValueAttribute = PropertyUtility.GetAttribute<MaxValueAttribute>(property);
            float max = ValueBoundUtility.Resolve(property, maxValueAttribute.MaxValueName, maxValueAttribute.MaxValue);

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (property.intValue > max)
                {
                    property.intValue = (int)max;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Float)
            {
                if (property.floatValue > max)
                {
                    property.floatValue = max;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Vector2)
            {
                property.vector2Value = Vector2.Min(property.vector2Value, new Vector2(max, max));
            }
            else if (property.propertyType == SerializedPropertyType.Vector3)
            {
                property.vector3Value = Vector3.Min(property.vector3Value, new Vector3(max, max, max));
            }
            else if (property.propertyType == SerializedPropertyType.Vector4)
            {
                property.vector4Value = Vector4.Min(property.vector4Value, new Vector4(max, max, max, max));
            }
            else if (property.propertyType == SerializedPropertyType.Vector2Int)
            {
                property.vector2IntValue = Vector2Int.Min(property.vector2IntValue, new Vector2Int((int)max, (int)max));
            }
            else if (property.propertyType == SerializedPropertyType.Vector3Int)
            {
                property.vector3IntValue = Vector3Int.Min(property.vector3IntValue, new Vector3Int((int)max, (int)max, (int)max));
            }
            else
            {
                string warning = maxValueAttribute.GetType().Name + " can be used only on int, float, Vector or VectorInt fields";
                Debug.LogWarning(warning, property.serializedObject.targetObject);
            }
        }
    }
}
