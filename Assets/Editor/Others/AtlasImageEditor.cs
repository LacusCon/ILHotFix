using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine.U2D;
using System.Linq;
using UnityEditor.AnimatedValues;
using UnityEngine.UI;

[CustomEditor(typeof(AtlasImage), true)]
[CanEditMultipleObjects]
public class AtlasImageEditor : ImageEditor {

    SerializedProperty m_Atlas;
    SerializedProperty m_SpriteName;

    AnimBool m_ShowSpriteName;

    string[] atlasSpriteNames;
    int spriteNameIndex = 0;

    protected override void OnEnable ()
    {
        m_Atlas = serializedObject.FindProperty ("m_Atlas");
        m_SpriteName = serializedObject.FindProperty ("m_SpriteName");
        m_ShowSpriteName = new AnimBool (m_Atlas.objectReferenceValue != null);
        m_ShowSpriteName.valueChanged.AddListener (Repaint);

        ResetAtlasSpriteNames ();
        ResetSpriteNameIndex ();
        base.OnEnable ();
    }

    protected override void OnDisable ()
    {
        m_ShowSpriteName.valueChanged.RemoveListener (Repaint);
        base.OnDisable ();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update ();
        AtlasGUI ();

        m_ShowSpriteName.target = m_Atlas.objectReferenceValue != null;
        if (EditorGUILayout.BeginFadeGroup (m_ShowSpriteName.faded))
            SpriteNameGUI ();
        EditorGUILayout.EndFadeGroup ();

        serializedObject.ApplyModifiedProperties ();

        base.OnInspectorGUI ();
    }

    protected virtual void AtlasGUI(){
        EditorGUI.BeginChangeCheck ();
        EditorGUILayout.PropertyField (m_Atlas);

        if (EditorGUI.EndChangeCheck ()) {
            ResetAtlasSpriteNames ();
            ResetSpriteNameIndex ();
        }
    }

    // 現在の名前からindexを求める
    void ResetSpriteNameIndex(){
        if (atlasSpriteNames == null || atlasSpriteNames.Length == 0)
            return;

        string currentName = m_SpriteName.stringValue;
        int tempIndex = 0;
        for (int i = 0; i < atlasSpriteNames.Length; i++) {
            if (currentName == atlasSpriteNames [i]) {
                tempIndex = i;
                break;
            }
        }
        spriteNameIndex = tempIndex;
        m_SpriteName.stringValue = atlasSpriteNames [spriteNameIndex];
        UpdateSourceImage ();
    }

    void ResetAtlasSpriteNames(){
        var newAtlas = m_Atlas.objectReferenceValue as SpriteAtlas;
        if (newAtlas) {
            atlasSpriteNames = GetAllSprite (newAtlas)
            .Select (x => x.name.Replace ("(Clone)", ""))
            .ToArray ();
        }
    }

    protected virtual void SpriteNameGUI(){
        EditorGUI.BeginChangeCheck ();
        if (atlasSpriteNames != null)
            spriteNameIndex = EditorGUILayout.Popup ("SpriteName", spriteNameIndex, atlasSpriteNames);

        if (EditorGUI.EndChangeCheck ()) {
            m_SpriteName.stringValue = atlasSpriteNames [spriteNameIndex];
            UpdateSourceImage ();
        }
    }


    protected virtual void UpdateSourceImage(){
        SerializedProperty m_Type = serializedObject.FindProperty("m_Type");
        SerializedProperty m_Sprite = serializedObject.FindProperty("m_Sprite");

        var currentAtlas = m_Atlas.objectReferenceValue as SpriteAtlas;

        if (currentAtlas == null)
            return;

        var newSprite = currentAtlas.GetSprite (m_SpriteName.stringValue);

        m_Sprite.objectReferenceValue = newSprite;
        if (newSprite)
        {
            Image.Type oldType = (Image.Type)m_Type.enumValueIndex;
            if (newSprite.border.SqrMagnitude() > 0)
            {
                m_Type.enumValueIndex = (int)Image.Type.Sliced;
            }
            else if (oldType == Image.Type.Sliced)
            {
                m_Type.enumValueIndex = (int)Image.Type.Simple;
            }
        }
    }

    static IEnumerable<Sprite> GetAllSprite(SpriteAtlas spriteAtlas){
        //spriteの空の配列を作成、サイズはAtlasに含まれるSpriteの数
        Sprite[] spriteArray = new Sprite[spriteAtlas.spriteCount];

        //spriteArrayに全Spriteを設定
        spriteAtlas.GetSprites(spriteArray);
        foreach (var sprite in spriteArray) {
            yield return sprite;
        }
    }
}