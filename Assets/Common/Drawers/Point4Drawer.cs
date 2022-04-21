using System;
using UnityEditor;
using UnityEngine;

using Common.Core.Numerics;

[CustomPropertyDrawer(typeof(Point4i))]
[CustomPropertyDrawer(typeof(Point4f))]
[CustomPropertyDrawer(typeof(Point4d))]
public class Point4Drawer : PropertyDrawer
{

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        label = EditorGUI.BeginProperty(pos, label, prop);
        var contentRect = EditorGUI.PrefixLabel(pos, GUIUtility.GetControlID(FocusType.Passive), label);

        var labels = new[]
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z"),
            new GUIContent("W")
        };

        var properties = new[]
        {
            prop.FindPropertyRelative("x"),
            prop.FindPropertyRelative("y"),
            prop.FindPropertyRelative("z"),
            prop.FindPropertyRelative("w")
        };

        PropertyDrawerUtil.DrawPropertyFieldsHorizontal(contentRect, labels, properties);

        EditorGUI.EndProperty();
    }

}

