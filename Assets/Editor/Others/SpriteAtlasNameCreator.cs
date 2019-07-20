//  SpriteAtlasNameCreator.cs
//  http://kan-kikuchi.hatenablog.com/entry/SpriteAtlasNameCreator
//
//  Created by kan.kikuchi on 2017.07.18.

using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// SpriteAtlasに含まれるSpriteの名前を管理する定数クラスを自動生成するクラス
/// </summary>
public class SpriteAtlasNameCreator : AssetPostprocessor
{
    //SpriteAtlasが入っているディレクトリのパス
    private const string SPRITE_ATLAS_PATH = "Assets"; //"/Resources/SpriteAtlas";

    //定数クラスを書き出すディレクトリのパス
    private const string CONSTNTS_CLASS_DIRECTORY_PATH = "Assets/Scripts/SpriteAtlasKey/";

    //=================================================================================
    //感知、判定
    //=================================================================================

    //ファイルに何らかの変更があった時に呼ばれる
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        //変更のあったアセットのパスをまとめる
        var assetsPathList = new List<string>();
        assetsPathList.AddRange(importedAssets); //インポートされた OR 変更があったファイル
        assetsPathList.AddRange(movedAssets); //場所が移動されたファイル(移動先のパス)

        //変更のあったアセットの中からSpriteAtlasディレクトリに入っているものだけ抜き出す
        var spriteAtlasPathList = assetsPathList.Where(asset => asset.Contains(SPRITE_ATLAS_PATH)).ToList();

        //全ファイルを確認し、SpriteAtlasであれば定数クラスを生成
        foreach (var spriteAtlasPath in spriteAtlasPathList)
        {
            CreateIfneeded(spriteAtlasPath);
        }
    }

    //=================================================================================
    //生成
    //=================================================================================

    //指定したディレクトリ内にある全SpriteAtlasの定数クラスを生成
    [MenuItem("Create/SpriteAtlasName")]
    private static void CreateAll()
    {
        //指定したディレクトリに入っている全ファイルを取得(子ディレクトリも含む)
        var filePathArray = Directory.GetFiles(SPRITE_ATLAS_PATH, "*", SearchOption.AllDirectories);

        //取得した全ファイルを確認し、SpriteAtlasであれば定数クラスを生成
        foreach (var filePath in filePathArray)
        {
            CreateIfneeded(filePath);
        }
    }

    //指定されたパスがSpriteAtlasであれば定数クラスを生成
    private static void CreateIfneeded(string filePath)
    {
        //拡張子からSpriteAtlasかどうか判定
        if (Path.GetExtension(filePath) != ".spriteatlas")
        {
            return;
        }

        //SpriteAtlasを取得
        var spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(filePath);

        //全Spriteを取得し、さらにその名前を取得(Cloneは除去する)
        var spriteArray = new Sprite[spriteAtlas.spriteCount];
        spriteAtlas.GetSprites(spriteArray);

        var cloneTextLength = "(Clone)".Length;
        var spriteNameDict = spriteArray.Select(sprite => sprite.name.Substring(0, sprite.name.Length - cloneTextLength)).ToDictionary(spriteName => spriteName);

        //定数クラスを作成
        ConstantsClassCreator.Create(spriteAtlas.name + "Key", spriteAtlas.name + "に含まれるSpriteの名前を定数で管理するクラス", CONSTNTS_CLASS_DIRECTORY_PATH, spriteNameDict);
    }
}