using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.U2D.Sprites;
using System.Linq;

namespace Necromancer.Editor
{
    /// <summary>
    /// 이미지 해상도가 달라도 인게임 캐릭터 크기를 자동으로 통일해주는 똑똑한 슬라이서
    /// </summary>
    public class SpriteAutoProcessor : AssetPostprocessor
    {
        // [설정] 캐릭터가 인게임(Scene)에서 가질 표준 세로 길이 (Unit 단위)
        // 2.5f 면 약 2.5미터 정도의 크기로 화면에 보입니다.
        private const float TARGET_UNIT_HEIGHT = 2.5f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("04.Sprites")) return;

            TextureImporter textureImporter = (TextureImporter)assetImporter;
            textureImporter.textureType = TextureImporterType.Sprite;
            
            if (assetPath.Contains("Move") || assetPath.Contains("Attack") || assetPath.Contains("Die"))
            {
                textureImporter.spriteImportMode = SpriteImportMode.Multiple;
            }
            else
            {
                textureImporter.spriteImportMode = SpriteImportMode.Single;
            }

            // 픽셀 아트 최적화
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.Contains("04.Sprites")) return;
            if (!assetPath.Contains("Move") && !assetPath.Contains("Attack") && !assetPath.Contains("Die")) return;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(assetImporter);
            dataProvider.InitSpriteEditorDataProvider();

            int width = texture.width;
            int height = texture.height;
            float aspectRatio = (float)width / height;

            int columns, rows;

            // [개선] 레이아웃 판정 로직
            if (assetPath.Contains("Icons"))
            {
                // 스킬 아이콘은 프롬프트 특성상 5개(1x5) 또는 10개(5x2) 격자가 많습니다.
                if (aspectRatio > 4.0f) { columns = 5; rows = 1; }
                else if (aspectRatio > 1.8f) { columns = 5; rows = 2; }
                else if (aspectRatio >= 0.9f && aspectRatio <= 1.1f) { columns = 5; rows = 2; } // 정사각형 AI 생성물 대응 (5x2 가정)
                else { columns = 2; rows = 2; }
            }
            else
            {
                // 일반 캐릭터 애니메이션 (1x4, 2x2 등)
                if (aspectRatio > 3.5f) { columns = 4; rows = 1; }
                else if (aspectRatio > 2.5f) { columns = 3; rows = 1; }
                else if (aspectRatio > 1.4f) { columns = 2; rows = 1; }
                else if (aspectRatio > 0.5f && aspectRatio < 2.0f) { columns = 2; rows = 2; }
                else { columns = 4; rows = 1; }
            }

            Debug.Log($"[SpriteAutoProcessor] {texture.name} 분석 - Ratio: {aspectRatio:F2}, Size: {width}x{height} -> 감지된 레이아웃: {columns}x{rows}");

            int sliceHeight = height / rows;

            /*
             * [수정 이유: 2번 방법 구현]
             * 2x2(607px)와 1x4(1728px)는 세로 픽셀 수(sliceHeight)가 다릅니다.
             * 이를 해결하기 위해 'PPU = 이미지 세로 픽셀 / 목표 월드 크기' 공식을 사용합니다.
             * 이렇게 하면 300px 짜리든 400px 짜리든 인게임에선 똑같이 2.5 Unit 크기로 보입니다.
             */
            TextureImporter textureImporter = (TextureImporter)assetImporter;
            float calculatedPPU = (float)sliceHeight / TARGET_UNIT_HEIGHT;
            
            // 너무 낮은 PPU는 방지 (최소 50 이상)
            textureImporter.spritePixelsPerUnit = Mathf.Max(50f, calculatedPPU);

            var spriteRects = dataProvider.GetSpriteRects();
            if (spriteRects != null && spriteRects.Length == (columns * rows))
            {
                // PPU만 바뀌었을 수도 있으므로 Apply 후 종료
                dataProvider.Apply();
                return; 
            }

            Debug.Log($"[SpriteAutoProcessor] {texture.name} 규격화 적용: {columns}x{rows}, PPU: {textureImporter.spritePixelsPerUnit:F1}");

            int sliceWidth = width / columns;
            var newRects = new List<SpriteRect>();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var rect = new SpriteRect();
                    rect.rect = new Rect(c * sliceWidth, (rows - 1 - r) * sliceHeight, sliceWidth, sliceHeight);
                    rect.name = $"{texture.name}_{newRects.Count}";
                    rect.alignment = SpriteAlignment.BottomCenter;
                    rect.pivot = new Vector2(0.5f, 0f);
                    newRects.Add(rect);
                }
            }

            dataProvider.SetSpriteRects(newRects.ToArray());
            dataProvider.Apply();

            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ForceReserializeAssets(new[] { assetPath });
            };
        }
    }
}
