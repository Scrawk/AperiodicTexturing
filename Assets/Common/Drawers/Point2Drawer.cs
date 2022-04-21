using System;
using UnityEditor;
using UnityEngine;

using Common.Core.Numerics;

[CustomPropertyDrawer(typeof(Point2i))]
[CustomPropertyDrawer(typeof(Point2f))]
[CustomPropertyDrawer(typeof(Point2d))]
public class Point2Drawer : PropertyDrawer
{

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        label = EditorGUI.BeginProperty(pos, label, prop);
        var contentRect = EditorGUI.PrefixLabel(pos, GUIUtility.GetControlID(FocusType.Passive), label);

        var labels = new[]
        {
            new GUIContent("X"),
            new GUIContent("Y")
        };

        var properties = new[]
        {
            prop.FindPropertyRelative("x"),
            prop.FindPropertyRelative("y")
        };

        PropertyDrawerUtil.DrawPropertyFieldsHorizontal(contentRect, labels, properties);

        EditorGUI.EndProperty();
    }

}
