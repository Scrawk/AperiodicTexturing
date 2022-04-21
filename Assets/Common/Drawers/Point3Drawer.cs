using System;
using UnityEditor;
using UnityEngine;

using Common.Core.Numerics;

[CustomPropertyDrawer(typeof(Point3i))]
[CustomPropertyDrawer(typeof(Point3f))]
[CustomPropertyDrawer(typeof(Point3d))]
public class Point3Drawer : PropertyDrawer
{

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        label = EditorGUI.BeginProperty(pos, label, prop);
        var contentRect = EditorGUI.PrefixLabel(pos, GUIUtility.GetControlID(FocusType.Passive), label);

        var labels = new[]
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z")
        };

        var properties = new[]
        {
            prop.FindPropertyRelative("x"),
            prop.FindPropertyRelative("y"),
            prop.FindPropertyRelative("z")
        };

        PropertyDrawerUtil.DrawPropertyFieldsHorizontal(contentRect, labels, properties);

        EditorGUI.EndProperty();
    }

}

