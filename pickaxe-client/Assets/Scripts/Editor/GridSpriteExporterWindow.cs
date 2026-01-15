using System.IO;
using UnityEditor;
using UnityEngine;

namespace InfinitePickaxe.Client.Editor
{
    public class GridSpriteExporterWindow : EditorWindow
    {
        private enum ExportMode
        {
            Grid,
            SlicedSprites
        }

        private DefaultAsset sourceFolder;
        private DefaultAsset outputFolder;
        private ExportMode exportMode = ExportMode.SlicedSprites;
        private int cellWidth = 128;
        private int cellHeight = 128;
        private int paddingX;
        private int paddingY;
        private int offsetX;
        private int offsetY;
        private int targetSize = 128;
        private bool padToSquare = true;
        private bool includeSubfolders;
        private bool skipEmptyCells = true;
        private bool overwriteExisting;

        [MenuItem("Tools/Sprite Exporter/Grid Slice To PNG")]
        private static void Open()
        {
            var window = GetWindow<GridSpriteExporterWindow>("Grid Sprite Exporter");
            window.minSize = new Vector2(420f, 360f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source/Output", EditorStyles.boldLabel);
            sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("Source Folder", sourceFolder, typeof(DefaultAsset), false);
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space();
            exportMode = (ExportMode)EditorGUILayout.EnumPopup("Export Mode", exportMode);

            if (exportMode == ExportMode.Grid)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
                cellWidth = Mathf.Max(1, EditorGUILayout.IntField("Cell Width", cellWidth));
                cellHeight = Mathf.Max(1, EditorGUILayout.IntField("Cell Height", cellHeight));
                paddingX = Mathf.Max(0, EditorGUILayout.IntField("Padding X", paddingX));
                paddingY = Mathf.Max(0, EditorGUILayout.IntField("Padding Y", paddingY));
                offsetX = Mathf.Max(0, EditorGUILayout.IntField("Offset X", offsetX));
                offsetY = Mathf.Max(0, EditorGUILayout.IntField("Offset Y", offsetY));
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Sprite Settings", EditorStyles.boldLabel);
                targetSize = Mathf.Max(1, EditorGUILayout.IntField("Target Size", targetSize));
                padToSquare = EditorGUILayout.Toggle("Pad To Square", padToSquare);
            }

            EditorGUILayout.Space();
            includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);
            skipEmptyCells = EditorGUILayout.Toggle("Skip Empty Cell", skipEmptyCells);
            overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", overwriteExisting);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(sourceFolder == null || outputFolder == null))
            {
                if (GUILayout.Button("Export"))
                {
                    Export();
                }
            }
        }

        private void Export()
        {
            var sourcePath = AssetDatabase.GetAssetPath(sourceFolder);
            var outputPath = AssetDatabase.GetAssetPath(outputFolder);

            if (!AssetDatabase.IsValidFolder(sourcePath) || !AssetDatabase.IsValidFolder(outputPath))
            {
                Debug.LogError("[GridSpriteExporter] Invalid source or output folder.");
                return;
            }

            var sourceAbs = Path.GetFullPath(sourcePath);
            var outputAbs = Path.GetFullPath(outputPath);
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(sourceAbs, "*.png", searchOption);

            if (files.Length == 0)
            {
                Debug.LogWarning("[GridSpriteExporter] No PNG files found.");
                return;
            }

            var outputAbsNormalized = outputAbs.Replace('\\', '/');
            var totalExported = 0;

            foreach (var file in files)
            {
                var fullPathNormalized = Path.GetFullPath(file).Replace('\\', '/');
                if (includeSubfolders && fullPathNormalized.StartsWith(outputAbsNormalized))
                {
                    continue;
                }

                var assetPath = ToAssetPath(fullPathNormalized);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                var originalReadable = importer.isReadable;
                var originalCompression = importer.textureCompression;

                try
                {
                    if (!originalReadable || originalCompression != TextureImporterCompression.Uncompressed)
                    {
                        importer.isReadable = true;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    }

                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture == null)
                    {
                        Debug.LogWarning($"[GridSpriteExporter] Texture not found: {assetPath}");
                        continue;
                    }

                    var baseName = Path.GetFileNameWithoutExtension(file);

                    if (exportMode == ExportMode.Grid)
                    {
                        var columns = (texture.width - offsetX + paddingX) / (cellWidth + paddingX);
                        var rows = (texture.height - offsetY + paddingY) / (cellHeight + paddingY);

                        if (columns <= 0 || rows <= 0)
                        {
                            Debug.LogWarning($"[GridSpriteExporter] Grid size invalid for {assetPath}");
                            continue;
                        }

                        for (var row = 0; row < rows; row++)
                        {
                            var yTop = offsetY + (row * (cellHeight + paddingY));
                            var y = texture.height - yTop - cellHeight;
                            if (y < 0)
                            {
                                continue;
                            }

                            for (var col = 0; col < columns; col++)
                            {
                                var x = offsetX + (col * (cellWidth + paddingX));
                                if (x + cellWidth > texture.width || y + cellHeight > texture.height)
                                {
                                    continue;
                                }

                                var pixels = texture.GetPixels(x, y, cellWidth, cellHeight);
                                if (skipEmptyCells && IsAllTransparent(pixels))
                                {
                                    continue;
                                }

                                var index = (row * columns) + col;
                                var fileName = $"{baseName}_{index}.png";
                                var outputFile = Path.Combine(outputAbs, fileName);

                                if (!overwriteExisting && File.Exists(outputFile))
                                {
                                    continue;
                                }

                                var cellTexture = new Texture2D(cellWidth, cellHeight, TextureFormat.RGBA32, false);
                                cellTexture.SetPixels(pixels);
                                cellTexture.Apply();

                                var png = cellTexture.EncodeToPNG();
                                Object.DestroyImmediate(cellTexture);

                                File.WriteAllBytes(outputFile, png);
                                totalExported++;
                            }
                        }
                    }
                    else
                    {
                        totalExported += ExportSlicedSprites(texture, assetPath, baseName, outputAbs);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GridSpriteExporter] Failed to export {assetPath}: {ex.Message}");
                }
                finally
                {
                    RestoreImporter(importer, originalReadable, originalCompression);
                }
            }

            AssetDatabase.Refresh();

            Debug.Log($"[GridSpriteExporter] Exported {totalExported} sprites to {outputPath}");
        }

        private int ExportSlicedSprites(Texture2D texture, string assetPath, string baseName, string outputAbs)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var exported = 0;

            foreach (var asset in assets)
            {
                if (asset is not Sprite sprite)
                {
                    continue;
                }

                var rect = sprite.rect;
                var rectX = Mathf.RoundToInt(rect.x);
                var rectY = Mathf.RoundToInt(rect.y);
                var rectW = Mathf.RoundToInt(rect.width);
                var rectH = Mathf.RoundToInt(rect.height);

                if (rectW <= 0 || rectH <= 0)
                {
                    continue;
                }

                var pixels = texture.GetPixels(rectX, rectY, rectW, rectH);
                if (skipEmptyCells && IsAllTransparent(pixels))
                {
                    continue;
                }

                var squareSize = padToSquare ? Mathf.Max(rectW, rectH) : Mathf.Max(1, rectW);
                var squareTexture = new Texture2D(squareSize, squareSize, TextureFormat.RGBA32, false);
                var clearPixels = new Color[squareSize * squareSize];
                squareTexture.SetPixels(clearPixels);

                var offsetX = padToSquare ? (squareSize - rectW) / 2 : 0;
                var offsetY = padToSquare ? (squareSize - rectH) / 2 : 0;
                squareTexture.SetPixels(offsetX, offsetY, rectW, rectH, pixels);
                squareTexture.Apply();

                Texture2D outputTexture = squareTexture;
                if (squareSize != targetSize)
                {
                    outputTexture = ScaleTexture(squareTexture, targetSize, targetSize);
                    Object.DestroyImmediate(squareTexture);
                }

                var fileName = $"{baseName}_{sprite.name}.png";
                var outputFile = Path.Combine(outputAbs, fileName);

                if (!overwriteExisting && File.Exists(outputFile))
                {
                    Object.DestroyImmediate(outputTexture);
                    continue;
                }

                var png = outputTexture.EncodeToPNG();
                Object.DestroyImmediate(outputTexture);

                File.WriteAllBytes(outputFile, png);
                exported++;
            }

            return exported;
        }

        private static bool IsAllTransparent(Color[] pixels)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0.001f)
                {
                    return false;
                }
            }

            return true;
        }

        private static string ToAssetPath(string fullPath)
        {
            var dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (!fullPath.StartsWith(dataPath))
            {
                return null;
            }

            return "Assets" + fullPath.Substring(dataPath.Length);
        }

        private static void RestoreImporter(TextureImporter importer, bool readable, TextureImporterCompression compression)
        {
            if (importer.isReadable == readable && importer.textureCompression == compression)
            {
                return;
            }

            importer.isReadable = readable;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }

        private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var previousFilter = source.filterMode;
            source.filterMode = FilterMode.Bilinear;

            var readWrite = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? RenderTextureReadWrite.sRGB
                : RenderTextureReadWrite.Default;
            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, readWrite);
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            source.filterMode = previousFilter;

            return result;
        }
    }
}
