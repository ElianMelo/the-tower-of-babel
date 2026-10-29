using System;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    /// <summary>Resolves a numeric bound from a member name on the property's owner.</summary>
    public static class ValueBoundUtility
    {
        public static float Resolve(SerializedProperty property, string memberName, float fallback)
        {
            if (string.IsNullOrEmpty(memberName))
            {
                return fallback;
            }

            object target = PropertyUtility.GetTargetObjectWithProperty(property);
            if (TryResolve(target, memberName, out float value))
            {
                return value;
            }

            Debug.LogWarning($"Could not resolve numeric bound '{memberName}' for '{property.name}'", property.serializedObject.targetObject);
            return fallback;
        }

        public static bool TryResolve(object target, string memberName, out float value)
        {
            value = 0f;
            if (target == null)
            {
                return false;
            }

            object raw = null;
            var field = ReflectionUtility.GetField(target, memberName);
            if (field != null)
            {
                raw = field.GetValue(target);
            }
            else
            {
                var prop = ReflectionUtility.GetProperty(target, memberName);
                if (prop != null && prop.CanRead)
                {
                    raw = prop.GetValue(target);
                }
                else
                {
                    var method = ReflectionUtility.GetMethod(target, memberName);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        raw = method.Invoke(target, null);
                    }
                }
            }

            if (raw is float || raw is int || raw is double || raw is long)
            {
                value = Convert.ToSingle(raw);
                return true;
            }

            return false;
        }
    }
}
