#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Necromancer.Editor
{
    /// <summary>
    /// [EDITOR] 04.Sprites/Enemy 의 PNG 스프라이트 시트에서 .anim 클립을 자동 생성하고
    /// Override Controller 까지 한 번에 연결합니다.
    ///
    /// PNG 명명 규칙: Enemy_{N:02}_{Move|Attack|Die}.png  (대소문자 무관)
    ///   예) Enemy_02_Move.png, Enemy_02_Attack.png, Enemy_02_Die.png
    ///
    /// 실행 메뉴: Necromancer → [Full Pipeline] Generate Enemy Animations + Controllers
    /// </summary>
    public class EnemyAnimationGenerator
    {
        private const string ENEMY_SPRITE_PATH = "Assets/00.Necromancer/04.Sprites/Enemy";
        private const float FRAME_RATE = 12f;

        [MenuItem("Necromancer/[Full Pipeline] Generate Enemy Animations + Controllers")]
        public static void RunFullPipeline()
        {
            int createdClips = GenerateMissingAnimClips();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EnemyControllerGenerator.GenerateAll();

            Debug.Log($"<color=lime><b>[EnemyPipeline]</b></color> 완료! " +
                      $"생성된 Animation Clip: {createdClips}개");
        }

        public static int GenerateMissingAnimClips()
        {
            // Enemy_로 시작하는 PNG 전체 대상 — Move/Attack/Die 이외 파일은 ExtractActionKeyword에서 필터링
            string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D Enemy_", new[] { ENEMY_SPRITE_PATH });
            int created = 0;

            for (int i = 0; i < pngGuids.Length; i++)
            {
                string pngPath = AssetDatabase.GUIDToAssetPath(pngGuids[i]);
                string fileName = Path.GetFileNameWithoutExtension(pngPath); // e.g. "Enemy_02_Move"

                string keyword = ExtractActionKeyword(fileName);
                if (keyword == null) continue;

                string animPath = Path.Combine(ENEMY_SPRITE_PATH, fileName + ".anim")
                                      .Replace('\\', '/');

                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath) != null) continue;

                List<Sprite> sprites = LoadSpritesFromPath(pngPath);
                if (sprites.Count == 0)
                {
                    Debug.LogWarning($"[EnemyAnimGen] 스프라이트 없음 (슬라이싱 필요?): {pngPath}");
                    continue;
                }

                AnimationClip clip = BuildClip(sprites, fileName);
                AssetDatabase.CreateAsset(clip, animPath);
                created++;
                Debug.Log($"[EnemyAnimGen] 생성: {animPath} ({sprites.Count} frames)");
            }

            return created;
        }

        private static List<Sprite> LoadSpritesFromPath(string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            List<Sprite> sprites = new List<Sprite>();
            foreach (var asset in assets)
            {
                if (asset is Sprite s) sprites.Add(s);
            }
            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return sprites;
        }

        private static AnimationClip BuildClip(List<Sprite> sprites, string clipName)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = clipName;
            clip.frameRate = FRAME_RATE;

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type         = typeof(SpriteRenderer),
                path         = "",
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                frames[i] = new ObjectReferenceKeyframe
                {
                    time  = i / FRAME_RATE,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
            return clip;
        }

        private static string ExtractActionKeyword(string fileName)
        {
            string lower = fileName.ToLower();
            if (lower.Contains("move"))   return "Move";
            if (lower.Contains("attack")) return "Attack";
            if (lower.Contains("die"))    return "Die";
            return null;
        }
    }
}
#endif
