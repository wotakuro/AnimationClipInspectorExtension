using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace UTJ
{
    /// <summary>
    /// AnimationClipのオブジェクトを自動的にセットするエディター拡張です
    /// 
    /// Reflectionを利用しているので、バージョンアップによってある日突然使えなくなってしまう可能性がある事をご了承ください。
    /// </summary>
    public class AnimationClipInspectorExtension : EditorWindow
    {
        /// メニューアイテム名
        private const string MenuName = "Tools/UTJ/AnimationClipInspectorExtention";
        /// 設定の保存場所
        private const string ConfigFile = "AnimationClipInspectorExtention.json";

        /// <summary>
        /// ルール
        /// </summary>
        [System.Serializable]
        private class Rule
        {
            [SerializeField]
            public string animationClipPathHead;
            [SerializeField]
            public string attachPrefabPath;
            public GameObject gameObject { get; set; }
        }

        /// <summary>
        /// Jsonに保存すためだけに用意した型
        /// </summary>
        [System.Serializable]
        private class ConfigFileFormat
        {
            [SerializeField]
            public List<Rule> rules;
        }
        // 適応するルール一覧
        private List<Rule> rules = new List<Rule>();
        private Editor prevAnimationClipEditor;
        private Vector2 scrollPos;
        // 保存確認用のDirtyフラグ
        private bool isDirtyForSave = false;
        // 設定反映用用のDirtyフラグ
        private bool isDirtyForEdit= false;

        /// <summary>
        /// メニューを押して 有効・無効を切り替えます
        /// </summary>
        [MenuItem(MenuName)]
        public static void Create()
        {
            EditorWindow.GetWindow<AnimationClipInspectorExtension>();
        }

        // ウィンドウ立ち上がり時
        void OnEnable()
        {
            LoadConfig();
        }

        // 消える時は保存するか確認する
        void OnDisable()
        {
            if (!isDirtyForSave) { return; }
            bool flag = EditorUtility.DisplayDialog("保存しますか？", "この内容を保存しますか？", "OK", "Cancel");
            if (flag)
            {
                SaveConfig();
            }
        }

        /// <summary>
        /// インターフェース
        /// </summary>
        void OnGUI()
        {
            EditorGUILayout.LabelField("AnimationClipのプレビュー画面を以下のルールでセットします");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("設定をセーブ", GUILayout.Width(120)))
            {
                SaveConfig();
            }
            if (GUILayout.Button("設定をロード", GUILayout.Width(120)))
            {
                LoadConfig();
            }
            EditorGUILayout.EndHorizontal();
            // メニューのヘッド部分
            EditorGUILayout.LabelField("");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AnimationClipのパス(先頭指定)");
            EditorGUILayout.LabelField("PreviewObject", GUILayout.Width(120.0f));
            EditorGUILayout.LabelField("", GUILayout.Width(20.0f));
            EditorGUILayout.EndHorizontal();
            // 実際のルール部分
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                int deleteIndex = -1;
                for (int i = 0; i < rules.Count; ++i)
                {
                    EditorGUILayout.BeginHorizontal();
                    var oldClipPathHead = rules[i].animationClipPathHead;
                    rules[i].animationClipPathHead = EditorGUILayout.TextField(rules[i].animationClipPathHead);
                    // ダーティフラグ
                    if (oldClipPathHead != rules[i].animationClipPathHead)
                    {
                        isDirtyForEdit = isDirtyForSave = true;
                    }
                    var oldGameObject = rules[i].gameObject;
                    rules[i].gameObject = EditorGUILayout.ObjectField(rules[i].gameObject, typeof(GameObject), false, GUILayout.Width(120.0f)) as GameObject;
                    // オブジェクトが変わっていたらパスを書き換えます
                    if (oldGameObject != rules[i].gameObject)
                    {
                        rules[i].attachPrefabPath = AssetDatabase.GetAssetPath(rules[i].gameObject);
                        isDirtyForEdit = isDirtyForSave = true;
                    }
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        deleteIndex = i;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                // 削除対応
                if (deleteIndex >= 0)
                {
                    rules.RemoveAt(deleteIndex);
                    isDirtyForEdit = isDirtyForSave = true;
                }
                if (GUILayout.Button("設定の追加"))
                {
                    rules.Add(new Rule());
                    isDirtyForEdit = isDirtyForSave = true;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// 設定を保存します
        /// </summary>
        private void SaveConfig()
        {
            ConfigFileFormat data = new ConfigFileFormat()
            {
                rules = this.rules
            };
            var jsonStr = JsonUtility.ToJson(data);
            System.IO.File.WriteAllText(ConfigFile, jsonStr);
            isDirtyForSave = false;
        }

        /// <summary>
        /// 設定から読み込みます
        /// </summary>
        private void LoadConfig()
        {
            if (!System.IO.File.Exists(ConfigFile))
            {
                rules = new List<Rule>();
                return;
            }
            string jsonStr = System.IO.File.ReadAllText(ConfigFile);
            ConfigFileFormat data = JsonUtility.FromJson<ConfigFileFormat>(jsonStr);
            this.rules = data.rules;

            foreach (var rule in rules)
            {
                rule.gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(rule.attachPrefabPath);
            }
            isDirtyForSave = false;
            isDirtyForEdit = true;
        }
        

        /// <summary>
        /// 自動更新処理
        /// </summary>
        void Update()
        {
            var animationClipEditor = GetAnimationClipEditor();

            if (animationClipEditor != null && prevAnimationClipEditor != animationClipEditor || isDirtyForEdit)
            {
                string filePath = AssetDatabase.GetAssetPath(animationClipEditor.target);
                GameObject gameObj = GetPreviewGameObjectFromClipPath(filePath);
                SetGameObjectToAnimationClipEditor(animationClipEditor, gameObj);
            }
            isDirtyForEdit = false;
            prevAnimationClipEditor = animationClipEditor;
        }

        /// <summary>
        /// AnimationClipのパスから適切な GameObjectを返します
        /// </summary>
        /// <param name="animationClipPath">AnimationClipのパスを指定します</param>
        /// <returns>AnimationClipのPreviewに適応すべき GameObjectを返します</returns>
        private GameObject GetPreviewGameObjectFromClipPath(string animationClipPath)
        {
            foreach(var rule in rules){
                if( animationClipPath.StartsWith( rule.animationClipPathHead )) {
                    if (rule.gameObject == null)
                    {
                        rule.gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(rule.attachPrefabPath);
                    }
                    return rule.gameObject;
                }
            }
            return null;
        }


        /// <summary>
        /// AnimationClipのInspector( AnimationCLipEditor)のオブジェクトを取得してきます
        /// </summary>
        /// <returns>存在していれば、AnimationCLipEditorのオブジェクトを返します</returns>
        private static Editor GetAnimationClipEditor()
        {
            var editors = ActiveEditorTracker.sharedTracker.activeEditors;
            foreach (var editor in editors)
            {
                if (editor.GetType().FullName == "UnityEditor.AnimationClipEditor")
                {
                    return editor;
                }
            }
            return null;
        }

        /// <summary>
        /// AnimationClipEditorのPreviewに対して、GameObjectを指定します
        /// </summary>
        /// <param name="animationClipEdiotr">GetAnimationClipEditorで、取得してきたEditorオブジェクトの指定</param>
        /// <param name="previewInstance">プレビューに表示したいオブジェクト</param>
        /// <returns></returns>
        private static bool SetGameObjectToAnimationClipEditor(Editor animationClipEdiotr, GameObject previewInstance)
        {
            if (animationClipEdiotr == null) { return false; }
            var type = animationClipEdiotr.GetType();
            var avatarPrevField = type.GetField("m_AvatarPreview", BindingFlags.Instance | BindingFlags.NonPublic);
            object avatarPrev = avatarPrevField.GetValue(animationClipEdiotr); // <= ここで null Exceptionの場合 Reflectionでとれなくなってます
            if (avatarPrev == null) { return false; }
            var avatarPrevType = avatarPrev.GetType();
            var setPreviewMethod = avatarPrevType.GetMethod("SetPreview", BindingFlags.Instance | BindingFlags.NonPublic);
            setPreviewMethod.Invoke(avatarPrev, new object[] { previewInstance });  // <= ここで null Exceptionの場合 Reflectionでとれなくなってます
            return true;
        }

    }
}