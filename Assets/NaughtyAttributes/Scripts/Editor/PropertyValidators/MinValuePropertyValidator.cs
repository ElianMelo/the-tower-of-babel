using UnityEngine;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class MinValuePropertyValidator : PropertyValidatorBase
    {
        public override void ValidateProperty(SerializedProperty property)
        {
            MinValueAttribute minValueAttribute = PropertyUtility.GetAttribute<MinValueAttribute>(property);
            float min = ValueBoundUtility.Resolve(property, minValueAttribute.MinValueName, minValueAttribute.MinValue);

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (property.intValue < min)
                {
                    property.intValue = (int)min;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Float)
            {
                if (property.floatValue < min)
                {
                    property.floatValue = min;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Vector2)
            {
                property.vector2Value = Vector2.Max(property.vector2Value, new Vector2(min, min));
            }
            else if (property.propertyType == SerializedPropertyType.Vector3)
            {
                property.vector3Value = Vector3.Max(property.vector3Value, new Vector3(min, min, min));
            }
            else if (property.propertyType == SerializedPropertyType.Vector4)
            {
                property.vector4Value = Vector4.Max(property.vector4Value, new Vector4(min, min, min, min));
            }
            else if (property.propertyType == SerializedPropertyType.Vector2Int)
            {
                property.vector2IntValue = Vector2Int.Max(property.vector2IntValue, new Vector2Int((int)min, (int)min));
            }
            else if (property.propertyType == SerializedPropertyType.Vector3Int)
            {
                property.vector3IntValue = Vector3Int.Max(property.vector3IntValue, new Vector3Int((int)min, (int)min, (int)min));
            }
            else
            {
                string warning = minValueAttribute.GetType().Name + " can be used only on int, float, Vector or VectorInt fields";
                Debug.LogWarning(warning, property.serializedObject.targetObject);
            }
        }
    }
}
