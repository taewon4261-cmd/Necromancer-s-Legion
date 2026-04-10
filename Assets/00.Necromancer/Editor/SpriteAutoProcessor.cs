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
            
            // [REVISION] "Icons" 폴더는 마스터의 수동 설정을 존중하기 위해 자동 Multiple 대상에서 제외합니다.
            bool shouldAutoMultiple = assetPath.Contains("Move") || 
                                     assetPath.Contains("Attack") || 
                                     assetPath.Contains("Die") || 
                                     assetPath.Contains("Sheet");

            if (shouldAutoMultiple)
            {
                textureImporter.spriteImportMode = SpriteImportMode.Multiple;
            }
            // 그 외의 경우, 이미 Multiple로 설정되어 있다면 건드리지 않습니다. (사용자 수동 설정 존중)
            else if (textureImporter.spriteImportMode == SpriteImportMode.Multiple)
            {
                // 유지
            }
            else
            {
                // 기본값은 Single로 두되, 사용자가 바꾸면 위 else if에서 걸러짐
                textureImporter.spriteImportMode = SpriteImportMode.Single;
            }

            // 픽셀 아트 최적화
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.Contains("04.Sprites")) return;
            
            // [REVISION] 단일 아이콘이 조각나는 참극을 방지하기 위해 "Icons"를 자동 슬라이싱 대상에서 제외합니다.
            bool isAutoSliceTarget = assetPath.Contains("Move") || 
                                     assetPath.Contains("Attack") || 
                                     assetPath.Contains("Die");
                                     
            if (!isAutoSliceTarget) return;

            ProcessAutoSlicing(texture);
        }

        private void ProcessAutoSlicing(Texture2D texture)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(assetImporter);
            if (dataProvider == null) return;
            
            dataProvider.InitSpriteEditorDataProvider();

            int width = texture.width;
            int height = texture.height;
            float aspectRatio = (float)width / height;

            int columns, rows;
            if (assetPath.Contains("Icons"))
            {
                if (aspectRatio > 4.0f) { columns = 5; rows = 1; }
                else if (aspectRatio > 1.8f) { columns = 5; rows = 2; }
                else if (aspectRatio >= 0.9f && aspectRatio <= 1.1f) { columns = 5; rows = 2; }
                else { columns = 2; rows = 2; }
            }
            else
            {
                if (aspectRatio > 3.5f) { columns = 4; rows = 1; }
                else if (aspectRatio > 2.5f) { columns = 3; rows = 1; }
                else if (aspectRatio > 1.4f) { columns = 2; rows = 1; }
                else if (aspectRatio > 0.5f && aspectRatio < 2.0f) { columns = 2; rows = 2; }
                else { columns = 4; rows = 1; }
            }

            TextureImporter textureImporter = (TextureImporter)assetImporter;
            int sliceHeight = height / rows;
            float calculatedPPU = (float)sliceHeight / TARGET_UNIT_HEIGHT;
            textureImporter.spritePixelsPerUnit = Mathf.Max(50f, calculatedPPU);

            var spriteRects = dataProvider.GetSpriteRects();
            if (spriteRects != null && spriteRects.Length == (columns * rows))
            {
                dataProvider.Apply();
                return; 
            }

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
                if (assetImporter != null) AssetDatabase.ForceReserializeAssets(new[] { assetPath });
            };
        }
    }
}

