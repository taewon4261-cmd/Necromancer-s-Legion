#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Necromancer.Editor
{
    /// <summary>
    /// [EDITOR] 04.Sprites/Minion 의 PNG 스프라이트 시트에서 .anim 클립을 자동 생성하고
    /// Override Controller 까지 한 번에 연결합니다.
    ///
    /// PNG 명명 규칙: Minion_{N:02}_{Move|Attack|Die}.png  (대소문자 무관)
    /// 실행 메뉴: Necromancer → [Full Pipeline] Generate Minion Animations + Controllers
    /// </summary>
    public class MinionAnimationGenerator
    {
        private const string MINION_SPRITE_PATH = "Assets/00.Necromancer/04.Sprites/Minion";
        private const float FRAME_RATE = 12f;

        [MenuItem("Necromancer/[Full Pipeline] Generate Minion Animations + Controllers")]
        public static void RunFullPipeline()
        {
            int createdClips = GenerateMissingAnimClips();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            MinionControllerGenerator.GenerateAll();

            Debug.Log($"<color=lime><b>[MinionPipeline]</b></color> 완료! " +
                      $"생성된 Animation Clip: {createdClips}개");
        }

        // 외부(MinionControllerGenerator 등)에서도 단독 호출 가능
        public static int GenerateMissingAnimClips()
        {
            string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D Minion_0", new[] { MINION_SPRITE_PATH });
            int created = 0;

            for (int i = 0; i < pngGuids.Length; i++)
            {
                string pngPath = AssetDatabase.GUIDToAssetPath(pngGuids[i]);
                string fileName = Path.GetFileNameWithoutExtension(pngPath); // e.g. "Minion_02_Move"

                // Move / Attack / Die 에 해당하는 파일만 처리 (Icon 등 제외)
                string keyword = ExtractActionKeyword(fileName);
                if (keyword == null) continue;

                string animPath = Path.Combine(MINION_SPRITE_PATH, fileName + ".anim")
                                      .Replace('\\', '/');

                // 이미 .anim 파일이 있으면 스킵
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath) != null) continue;

                // PNG 에서 Sprite 목록 로드
                List<Sprite> sprites = LoadSpritesFromPath(pngPath);
                if (sprites.Count == 0)
                {
                    Debug.LogWarning($"[MinionAnimGen] 스프라이트 없음 (슬라이싱 필요?): {pngPath}");
                    continue;
                }

                AnimationClip clip = BuildClip(sprites, fileName, loop: true); // Move/Attack/Die 모두 루프
                AssetDatabase.CreateAsset(clip, animPath);
                created++;
                Debug.Log($"[MinionAnimGen] 생성: {animPath} ({sprites.Count} frames)");
            }

            return created;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static List<Sprite> LoadSpritesFromPath(string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            List<Sprite> sprites = new List<Sprite>();
            foreach (var asset in assets)
            {
                if (asset is Sprite s) sprites.Add(s);
            }
            // 이름 기준 정렬 (Minion_02_Move_0, _1, _2 ... 순서 보장)
            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return sprites;
        }

        private static AnimationClip BuildClip(List<Sprite> sprites, string clipName, bool loop)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = clipName;
            clip.frameRate = FRAME_RATE;

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type       = typeof(SpriteRenderer),
                path       = "",
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

        // 파일명에서 Move / Attack / Die 키워드 추출 (대소문자 무관)
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
