using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Image2UGUI.FontManagement
{
    // Explicit batchmode entry point. Never runs on import or in the user's working scene.
    public static class FontManagerVerification
    {
        const string Output = "Logs/FontManagerValidation";
        static readonly List<string> results = new List<string>();
        static void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); results.Add("PASS: " + name); }

        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("FONT_MANAGER_VALIDATION") != "1") throw new InvalidOperationException("Run only in an isolated validation project with FONT_MANAGER_VALIDATION=1.");
            Directory.CreateDirectory(Output);
            try
            {
                var codes = FontCharacterCollector.CodePoints("A\U00020000e\u0301\nA").ToArray();
                Check(codes.Contains(0x20000u) && codes.Contains(0x301u) && codes.Length == 5, "Unicode supplementary/combining/control handling");
                Check(FontCharacterCollector.VisibleText("<b>你好</b> <noparse><b>x</b></noparse> a < b {score:N0}", true, true) == "你好 <b>x</b> a < b ", "Rich text, noparse, literal comparisons and placeholders");
                bool invalid = false; try { FontCharacterCollector.CodePoints("\ud800").ToArray(); } catch (ArgumentException) { invalid = true; }
                Check(invalid, "Invalid surrogate rejected");
                FontBakeService.EnsureFolders();
                TestMissingGlyphsAndCurrentSource();
                var fonts = AssetDatabase.FindAssets("t:Font", new[] { "Assets/Fonts/Chinese" }).Select(AssetDatabase.GUIDToAssetPath).Select(p => AssetDatabase.LoadAssetAtPath<Font>(p)).Where(f => f != null).ToArray();
                Check(fonts.Length == 3, "Three source fonts present");
                TestTmpAssetModel(fonts[0], fonts.First(f => f != fonts[0]));
                TestWindowTmpWorkflows(fonts[0]);
                foreach (var source in fonts)
                {
                    var profile = ScriptableObject.CreateInstance<FontBakeProfile>();
                    profile.sourceFont = source; profile.includeCommonCharacters = false; profile.manualText = "开始游戏 设置 背包\n欢迎来到冒险世界！\nLevel 12 HP 100/100 金币 +2,500";
                    var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Fonts/Profiles/" + source.name + "_Test.asset");
                    AssetDatabase.CreateAsset(profile, path);
                    // A batch test has no serialized EditorWindow reference to keep the profile alive across NewScene.
                    profile.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                    var set = FontCharacterCollector.Collect(profile);
                    var asset = FontBakeService.Bake(profile, set);
                    Check(asset.atlasPopulationMode == AtlasPopulationMode.Static && asset.sourceFontFile == null, source.name + " static output without runtime source font");
                    Check(set.Codes.All(c => asset.characterLookupTable.ContainsKey(c)), source.name + " complete character coverage");
                    Check(asset.material.mainTexture == asset.atlasTextures[0] && AssetDatabase.IsSubAsset(asset.material), source.name + " persistent material/atlas");
                    string assetPath = AssetDatabase.GetAssetPath(asset);
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    string fontId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
                    string materialId = GlobalObjectId.GetGlobalObjectIdSlow(asset.material).ToString();
                    string textureId = GlobalObjectId.GetGlobalObjectIdSlow(asset.atlasTextures[0]).ToString();
                    profile.manualText += " 新增文字";
                    var updated = FontBakeService.Bake(profile, FontCharacterCollector.Collect(profile));
                    Check(updated == asset && GlobalObjectId.GetGlobalObjectIdSlow(updated).ToString() == fontId && AssetDatabase.AssetPathToGUID(assetPath) == guid, source.name + " font GUID/local ID preserved");
                    Check(GlobalObjectId.GetGlobalObjectIdSlow(updated.material).ToString() == materialId && GlobalObjectId.GetGlobalObjectIdSlow(updated.atlasTextures[0]).ToString() == textureId, source.name + " material/atlas local IDs preserved");
                    Check(updated.characterLookupTable.ContainsKey('新'), source.name + " updated character dictionary");
                    var saved = File.ReadAllBytes(assetPath);
                    profile.manualText += "\U0010ffff";
                    bool rejected = false; try { FontBakeService.Bake(profile, FontCharacterCollector.Collect(profile)); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && File.ReadAllBytes(assetPath).SequenceEqual(saved), source.name + " missing glyph failure leaves previous asset intact");
                    profile.manualText = "开始游戏 设置 背包\n欢迎来到冒险世界！\nLevel 12 HP 100/100 金币 +2,500";
                    profile.style.outlineWidth = .15f; profile.style.shadow = true;
                    profile.style.width = 720;
                    using (var preview = FontPreviewRenderer.Render(asset, profile.manualText, profile.style, false))
                    {
                        File.WriteAllBytes(Output + "/" + source.name + ".png", preview.image.EncodeToPNG());
                        var pixels = preview.image.GetPixels32();
                        Check(pixels.Select(c => c.r).Distinct().Count() > 10, source.name + " actual TMP rendered pixels");
                        Check(preview.lines >= 3 && !preview.overflow, source.name + " expected lines fit normal frame");
                    }
                    profile.style.width = 64; profile.style.height = 32;
                    using (var preview = FontPreviewRenderer.Render(asset, profile.manualText, profile.style, false)) Check(preview.overflow, source.name + " small frame overflow detected");
                    profile.style.width = 480; profile.style.height = 180;
                    if (source == fonts[0]) TestSourcesAndApply(profile);
                    profile.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
                    EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                }
                // Force a genuine packing failure with supported characters in a small atlas.
                var stress = ScriptableObject.CreateInstance<FontBakeProfile>(); stress.sourceFont = fonts[0]; stress.includeCommonCharacters = false;
                stress.samplingSize = 200; stress.padding = 32; stress.atlasSize = 512;
                stress.manualText = string.Concat(Enumerable.Range(33, 90).Select(i => (char)i));
                bool overflowRejected = false; try { FontBakeService.Bake(stress, FontCharacterCollector.Collect(stress)); } catch (InvalidOperationException e) { overflowRejected = e.Message.Contains("图集"); }
                Check(overflowRejected && stress.generatedFont == null, "Atlas capacity failure produces no incomplete output");
                Object.DestroyImmediate(stress);
                File.WriteAllLines(Output + "/results.txt", results);
                Debug.Log("FONT_MANAGER_VERIFICATION_PASS " + results.Count);
            }
            catch (Exception e)
            {
                results.Add(e.ToString()); File.WriteAllLines(Output + "/results.txt", results); Debug.LogException(e); EditorApplication.Exit(1);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void TestMissingGlyphsAndCurrentSource()
        {
            var profile = ScriptableObject.CreateInstance<FontBakeProfile>();
            var window = ScriptableObject.CreateInstance<FontManagerWindow>();
            TMP_FontAsset transient = null;
            try
            {
                var anton = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Examples & Extras/Fonts/Anton.ttf");
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Chinese/SourceHanSansCN-Regular.otf");
                var alternate = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Chinese/SourceHanSerifCN-Regular.otf");
                if (anton == null || source == null || alternate == null) throw new InvalidOperationException("Regression source fonts are missing.");
                typeof(FontManagerWindow).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, profile);
                string defaultText = profile.manualText;
                profile.sourceFont = anton; profile.manualText = "开始游戏 ABC123"; profile.includeCommonCharacters = false;
                var set = FontCharacterCollector.Collect(profile);
                var coverage = FontBakeService.Check(anton, set.Codes, null);
                Check(coverage.unsupported.SetEquals(FontCharacterCollector.CodePoints("开始游戏")) && coverage.unbaked.SetEquals(FontCharacterCollector.CodePoints(" ABC123")), "Anton distinguishes missing Chinese glyphs from supported unbaked ASCII");
                string error = null;
                try { transient = FontBakeService.BuildTransient(anton, set.Codes, profile.samplingSize, profile.padding, profile.atlasSize, profile.renderMode); }
                catch (InvalidOperationException e) { error = e.Message; }
                Check(error != null && error.Contains(anton.name) && error.Contains("字形") && (error.Contains("定位来源") || error.Contains("fallback")) && error.Length < 320 && !error.Contains("U+"), "Missing glyph error names the source and directs the developer to locate/fallback");
                var existingAssets = AssetDatabase.FindAssets("", new[] { "Assets/Fonts/Generated" }).OrderBy(g => g).ToArray();
                bool rejected = false;
                try { FontBakeService.Bake(profile, set); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && profile.generatedFont == null && existingAssets.SequenceEqual(AssetDatabase.FindAssets("", new[] { "Assets/Fonts/Generated" }).OrderBy(g => g)), "Missing glyph generation creates no persistent assets");
                WindowCall(window, "Collect");
                string summary = (string)WindowCall(window, "ReportSummary");
                Check(summary.Contains("字形") && (summary.Contains("缺") || summary.Contains("不包含") || summary.Contains("未覆盖")) && !summary.Contains("旧字体") && !summary.Contains("另一款"), "Unbaked Anton report explains missing glyphs without claiming an old font");

                // Exercise the exact new-profile defaults, including currency and runtime symbols.
                profile.sourceFont = source; profile.manualText = defaultText; profile.includeCommonCharacters = true;
                set = FontCharacterCollector.Collect(profile);
                Check(set.Codes.Length == 64, "Default Chinese profile collects all 64 unique characters");
                WindowCall(window, "Collect");
                coverage = WindowCoverage(window);
                Check(coverage.unsupported.Count == 0 && coverage.unbaked.SetEquals(set.Codes), "Default Chinese characters are supported and await generation");
                summary = (string)WindowCall(window, "ReportSummary");
                Check((summary.Contains("生成") || summary.Contains("烘焙")) && (summary.Contains("待") || summary.Contains("未") || summary.Contains("没有")) && !summary.Contains("旧字体") && !summary.Contains("另一款"), "Supported characters without TMP explain pending generation");
                AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath("Assets/Fonts/Profiles/DefaultSourceRegression.asset"));
                var generated = FontBakeService.Bake(profile, set);
                Check(generated.atlasPopulationMode == AtlasPopulationMode.Static && set.Codes.All(c => generated.characterLookupTable.ContainsKey(c)), "Default 64-character configuration bakes a complete static TMP asset");
                Check(profile.lastBakedSourceFontGuid == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)), "Bake records the source font GUID");
                WindowCall(window, "Collect"); WindowCall(window, "RequireCurrent");
                Check(WindowCoverage(window).unbaked.Count == 0, "Freshly baked current font can be applied");

                profile.sourceFont = alternate;
                WindowCall(window, "Collect"); coverage = WindowCoverage(window);
                Check(coverage.unsupported.Count == 0 && coverage.unbaked.SetEquals(set.Codes), "Switching to a covered Chinese font marks every character unbaked");
                Check(CurrentRejected(window), "A different source font cannot apply the old generated asset");
                profile.sourceFont = source;
                WindowCall(window, "Collect"); WindowCall(window, "RequireCurrent");
                Check(WindowCoverage(window).unbaked.Count == 0, "Switching back restores the valid generated asset");

                profile.lastBakedSourceFontGuid = "";
                WindowCall(window, "Collect"); WindowCall(window, "RequireCurrent");
                Check(WindowCoverage(window).unbaked.Count == 0, "Legacy profile with matching signature remains valid without source GUID");
                profile.samplingSize += 1;
                WindowCall(window, "Collect");
                Check(WindowCoverage(window).unbaked.Count == 0 && !CurrentRejected(window), "Legacy profile with changed bake settings still allows applying the existing TMP");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                FontBakeService.DestroyTransient(transient);
                Object.DestroyImmediate(window);
                if (!EditorUtility.IsPersistent(profile)) Object.DestroyImmediate(profile);
            }
        }

        static void TestTmpAssetModel(Font source, Font fallbackSource)
        {
            var createdProfiles = new List<string>();
            var createdFonts = new List<string>();
            FontBakeProfile primaryProfile = null;
            FontBakeProfile fallbackProfile = null;
            TMP_FontAsset fallbackAsset = null;
            TMP_FontAsset dynamicFallbackAsset = null;
            TMP_FontAsset transient = null;
            TMP_FontAsset singleAtlasCandidate = null;
            try
            {
                string fallbackProfilePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Fonts/Profiles/TmpAssetModel_Fallback.asset");
                fallbackProfile = ScriptableObject.CreateInstance<FontBakeProfile>();
                fallbackProfile.sourceFont = fallbackSource;
                fallbackProfile.manualText = "字";
                fallbackProfile.includeCommonCharacters = false;
                AssetDatabase.CreateAsset(fallbackProfile, fallbackProfilePath); createdProfiles.Add(fallbackProfilePath);
                fallbackAsset = FontBakeService.Bake(fallbackProfile, FontCharacterCollector.Collect(fallbackProfile));
                string fallbackAssetPath = AssetDatabase.GetAssetPath(fallbackAsset); createdFonts.Add(fallbackAssetPath);
                Check(fallbackAsset.atlasPopulationMode == AtlasPopulationMode.Static && fallbackAsset.sourceFontFile == null, "Static TMP clears runtime TTF reference");
                Check(FontBakeService.GetReferencedSourceFont(fallbackAsset) == fallbackSource, "Static TMP retains editor source TTF reference");
                var fallbackInfo = FontBakeService.Inspect(fallbackAsset);
                Check(fallbackInfo.sourceFont == fallbackSource && fallbackInfo.sourceFontPath == AssetDatabase.GetAssetPath(fallbackSource), "TMP inspection resolves source TTF path");
                Check(fallbackInfo.atlases.Count == 1 && fallbackInfo.atlases[0].assetPath == fallbackAssetPath && fallbackInfo.materialPath == fallbackAssetPath, "TMP inspection resolves atlas and material sub-assets");
                Check(FontBakeService.DescribeAsset(fallbackAsset).Contains("源 TTF") && FontBakeService.DescribeAsset(fallbackAsset).Contains("图集"), "TMP inspection summary names source and atlas");

                string primaryProfilePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Fonts/Profiles/TmpAssetModel_Primary.asset");
                primaryProfile = ScriptableObject.CreateInstance<FontBakeProfile>();
                primaryProfile.sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Examples & Extras/Fonts/Anton.ttf");
                primaryProfile.manualText = "A";
                primaryProfile.includeCommonCharacters = false;
                primaryProfile.fallbackFontAssets.Add(fallbackAsset);
                AssetDatabase.CreateAsset(primaryProfile, primaryProfilePath); createdProfiles.Add(primaryProfilePath);
                var fallbackCode = FontCharacterCollector.CodePoints("字").Single();
                var primaryCodes = FontCharacterCollector.CodePoints("A字").ToArray();
                var coverage = FontBakeService.Check(primaryProfile.sourceFont, primaryCodes, null, new[] { fallbackAsset });
                Check(!coverage.unsupported.Contains(fallbackCode) && coverage.resolvedByFallback[fallbackCode] == fallbackAsset, "Static fallback resolves glyph absent from primary TTF");
                Check(coverage.unbaked.Contains((uint)'A'), "Primary supported glyph remains pending bake");
                fallbackAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallbackAsset };
                coverage = FontBakeService.Check(primaryProfile.sourceFont, new[] { fallbackCode }, null, new[] { fallbackAsset });
                Check(coverage.resolvedByFallback[fallbackCode] == fallbackAsset, "Fallback self-cycle terminates and still resolves glyph");

                // Dynamic fallback is allowed to resolve a character through its
                // own source TTF before that character is present in its atlas.
                var dynamicProfile = ScriptableObject.CreateInstance<FontBakeProfile>();
                dynamicProfile.sourceFont = fallbackSource; dynamicProfile.populationMode = AtlasPopulationMode.Dynamic;
                dynamicProfile.manualText = "A"; dynamicProfile.includeCommonCharacters = false;
                dynamicFallbackAsset = FontBakeService.BuildTransient(fallbackSource, FontCharacterCollector.CodePoints("A").ToArray(), dynamicProfile.samplingSize, dynamicProfile.padding, dynamicProfile.atlasSize, dynamicProfile.renderMode, AtlasPopulationMode.Dynamic, false, null);
                var dynamicCoverage = FontBakeService.Check(primaryProfile.sourceFont, new[] { fallbackCode }, null, new[] { dynamicFallbackAsset });
                Check(dynamicCoverage.resolvedByFallback[fallbackCode] == dynamicFallbackAsset, "Dynamic fallback resolves glyph from referenced TTF");
                TestDynamicPreviewIsolation(primaryProfile.sourceFont, dynamicFallbackAsset);
                Object.DestroyImmediate(dynamicProfile);

                // A multi-atlas candidate must retain every texture when saved.
                var multiProfile = ScriptableObject.CreateInstance<FontBakeProfile>();
                var multiSource = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Chinese/SourceHanSansCN-Regular.otf") ?? source;
                multiProfile.sourceFont = multiSource; multiProfile.samplingSize = 200; multiProfile.padding = 32; multiProfile.atlasSize = 512; multiProfile.enableMultiAtlas = true; multiProfile.includeCommonCharacters = false;
                multiProfile.manualText = string.Concat(Enumerable.Range(0x4E00, 180).Select(i => (char)i));
                string multiProfilePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Fonts/Profiles/TmpAssetModel_Multi.asset");
                AssetDatabase.CreateAsset(multiProfile, multiProfilePath); createdProfiles.Add(multiProfilePath);
                var multiSet = FontCharacterCollector.Collect(multiProfile);
                var multiAsset = FontBakeService.Bake(multiProfile, multiSet);
                string multiAssetPath = AssetDatabase.GetAssetPath(multiAsset); createdFonts.Add(multiAssetPath);
                Check(multiAsset.atlasTextures.Length > 1, "Multi-atlas candidate creates more than one atlas");
                Check(multiAsset.atlasTextures.All(t => t != null && AssetDatabase.IsSubAsset(t) && AssetDatabase.GetAssetPath(t) == multiAssetPath), "Multi-atlas persistence keeps every texture sub-asset");
                Check(FontBakeService.Inspect(multiAsset).atlases.Count == multiAsset.atlasTextures.Length, "Multi-atlas inspection reports every texture");

                singleAtlasCandidate = FontBakeService.BuildTransient(multiSource, multiSet.Codes, 64, 4, 2048, GlyphRenderMode.SDFAA, AtlasPopulationMode.Static, false, null);
                bool canUpdate = FontBakeService.CanUpdateInPlace(multiAsset, singleAtlasCandidate, out string updateReason);
                Check(!canUpdate && updateReason.Contains("图集数量"), "Atlas count change refuses unsafe in-place update");
                FontBakeService.DestroyTransient(singleAtlasCandidate); singleAtlasCandidate = null;
            }
            finally
            {
                FontBakeService.DestroyTransient(transient);
                FontBakeService.DestroyTransient(dynamicFallbackAsset);
                FontBakeService.DestroyTransient(singleAtlasCandidate);
                foreach (var path in createdFonts) if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
                foreach (var path in createdProfiles) if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
        }

        static void TestDynamicPreviewIsolation(Font primarySource, TMP_FontAsset dynamicAsset)
        {
            TMP_FontAsset primary = null;
            var globals = TMP_Settings.fallbackFontAssets;
            var savedGlobals = globals == null ? null : globals.ToArray();
            try
            {
                var style = new FontPreviewStyle();
                var before = CaptureAtlasState(dynamicAsset);
                using (var preview = FontPreviewRenderer.Render(dynamicAsset, "A新增字形", style, false))
                    Check(preview.image != null, "Dynamic primary renders through an isolated preview copy");
                Check(before == CaptureAtlasState(dynamicAsset), "Dynamic primary preview preserves original characters and atlas pixels");

                primary = FontBakeService.BuildTransient(primarySource, new[] { (uint)'A' }, 64, 8, 1024, GlyphRenderMode.SDFAA);
                primary.fallbackFontAssetTable = new List<TMP_FontAsset> { dynamicAsset };
                before = CaptureAtlasState(dynamicAsset);
                using (var preview = FontPreviewRenderer.Render(primary, "A字", style, false))
                    Check(preview.image != null, "Dynamic fallback renders through an isolated preview copy");
                Check(before == CaptureAtlasState(dynamicAsset), "Dynamic local fallback preview preserves original characters and atlas pixels");

                Check(globals != null, "TMP settings exposes a global fallback list for preview isolation");
                primary.fallbackFontAssetTable.Clear();
                globals.Clear(); globals.Add(dynamicAsset);
                before = CaptureAtlasState(dynamicAsset);
                using (var preview = FontPreviewRenderer.Render(primary, "A全局", style, false))
                    Check(preview.image != null, "Dynamic global fallback renders through an isolated preview copy");
                Check(before == CaptureAtlasState(dynamicAsset), "Dynamic global fallback preview preserves original characters and atlas pixels");
                Check(globals.Count == 1 && globals[0] == dynamicAsset, "Preview restores the original global fallback references");
            }
            finally
            {
                if (globals != null && savedGlobals != null) { globals.Clear(); globals.AddRange(savedGlobals); }
                FontBakeService.DestroyTransient(primary);
            }
        }

        static string CaptureAtlasState(TMP_FontAsset font)
        {
            string tables = string.Join(",", font.characterTable.Select(c => c.unicode + ":" + c.glyphIndex));
            string textures = string.Join("|", (font.atlasTextures ?? Array.Empty<Texture2D>()).Select(t => t == null ? "null" : t.GetInstanceID() + ":" + t.width + "x" + t.height + ":" + Convert.ToBase64String(t.GetRawTextureData())));
            return tables + "|" + textures;
        }

        static void TestWindowTmpWorkflows(Font source)
        {
            var profile = ScriptableObject.CreateInstance<FontBakeProfile>();
            var window = ScriptableObject.CreateInstance<FontManagerWindow>();
            TMP_FontAsset noSource = null;
            string profilePath = null, fontPath = null;
            try
            {
                profile.sourceFont = source; profile.manualText = "原"; profile.includeCommonCharacters = false;
                profilePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Fonts/Profiles/TmpWindowRegression.asset");
                AssetDatabase.CreateAsset(profile, profilePath);
                var asset = FontBakeService.Bake(profile, FontCharacterCollector.Collect(profile));
                fontPath = AssetDatabase.GetAssetPath(asset);
                WindowSet(window, "profile", profile);
                profile.generatedFont = null; profile.sourceFont = null;
                WindowCall(window, "SelectTmp", asset);
                var selected = (TMP_FontAsset)typeof(FontManagerWindow).GetProperty("selectedTmp", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                Check(profile.generatedFont == asset && selected == asset && profile.sourceFont == source, "Selecting TMP uses one profile target and resolves its source TTF");

                profile.manualText = "新";
                WindowCall(window, "Bake");
                Check(profile.generatedFont == asset && asset.characterLookupTable.ContainsKey('原') && asset.characterLookupTable.ContainsKey('新'), "Supplementing current TMP preserves its existing characters and references");

                WindowSet(window, "optimizationSamplingSize", 48);
                WindowSet(window, "optimizationPadding", 4);
                WindowSet(window, "optimizationAtlasSize", 1024);
                WindowCall(window, "BuildOptimizationPreview");
                var candidate = (TMP_FontAsset)WindowGet(window, "optimizedPreviewFont");
                Check(candidate != null && candidate != asset && !EditorUtility.IsPersistent(candidate), "Optimization creates a transient candidate without replacing current TMP");
                Check(candidate.characterLookupTable.ContainsKey('原') && candidate.characterLookupTable.ContainsKey('新'), "Default optimization candidate retains existing TMP character coverage");
                string originalState = CaptureAtlasState(asset);
                WindowSet(window, "optimizationPadding", 5);
                Check(WindowRejected(window, "ApplyOptimizationCandidate") && profile.generatedFont == asset && CaptureAtlasState(asset) == originalState, "Changed optimization parameters reject applying stale candidate and preserve current TMP");

                noSource = FontBakeService.BuildTransient(source, new[] { (uint)'A' }, 64, 8, 1024, GlyphRenderMode.SDFAA);
                var serialized = new SerializedObject(noSource);
                foreach (var field in new[] { "m_SourceFontFile", "m_SourceFontFile_EditorRef" })
                {
                    var property = serialized.FindProperty(field); if (property != null) property.objectReferenceValue = null;
                }
                var sourceGuid = serialized.FindProperty("m_SourceFontFileGUID"); if (sourceGuid != null) sourceGuid.stringValue = string.Empty;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var creation = noSource.creationSettings; creation.sourceFontFileGUID = string.Empty; noSource.creationSettings = creation;
                WindowCall(window, "SelectTmp", noSource);
                Check(profile.sourceFont == null, "Static TMP fixture has no editor or runtime source TTF");
                profile.manualText = "A字";
                WindowCall(window, "Collect");
                var report = WindowCoverage(window);
                Check(!report.unsupported.Contains('A') && !report.unbaked.Contains('A') && report.unsupported.Contains('字'), "Static TMP without a source TTF still diagnoses existing and absent glyphs");
                profile.manualText = "A";
                Check(!CurrentRejected(window), "Static TMP without a source TTF can apply already-baked characters");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(window);
                FontBakeService.DestroyTransient(noSource);
                if (!string.IsNullOrEmpty(fontPath)) AssetDatabase.DeleteAsset(fontPath);
                if (!string.IsNullOrEmpty(profilePath)) AssetDatabase.DeleteAsset(profilePath);
                else Object.DestroyImmediate(profile);
            }
        }

        static object WindowCall(FontManagerWindow window, string method, params object[] arguments) => typeof(FontManagerWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, arguments);
        static object WindowGet(FontManagerWindow window, string field) => typeof(FontManagerWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        static void WindowSet(FontManagerWindow window, string field, object value) => typeof(FontManagerWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, value);
        static bool WindowRejected(FontManagerWindow window, string method)
        {
            try { WindowCall(window, method); return false; }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { return true; }
        }
        static FontCoverage WindowCoverage(FontManagerWindow window) => (FontCoverage)typeof(FontManagerWindow).GetField("coverage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        static bool CurrentRejected(FontManagerWindow window)
        {
            try { WindowCall(window, "RequireCurrent"); return false; }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { return true; }
        }

        static void TestSourcesAndApply(FontBakeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("SourceRoot", typeof(RectTransform));
            var text = root.AddComponent<TextMeshProUGUI>(); text.font = profile.generatedFont; text.text = "<b>源</b>";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Fonts/Profiles/TextSource.prefab");
            EditorSceneManager.SaveScene(scene, "Assets/Fonts/Profiles/TextSource.unity");
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Fonts/Profiles/TextSource.unity");
            profile.contentAssets.Add(prefab); profile.contentAssets.Add(sceneAsset);
            var set = FontCharacterCollector.Collect(profile);
            Check(set.characters.ContainsKey('源') && !set.characters.ContainsKey('<'), "Scene and Prefab text collection respects component rich text");
            Check(set.characters['源'].Count == 2, "Both scene and prefab origins retained");
            EditorSceneManager.MarkSceneDirty(scene);
            bool dirtyRejected = false; try { FontCharacterCollector.Collect(profile); } catch (InvalidOperationException) { dirtyRejected = true; }
            Check(dirtyRejected, "Unsaved scene protected");
            EditorSceneManager.SaveScene(scene);
            FontBakeService.Bake(profile, FontCharacterCollector.Collect(profile));
            var window = ScriptableObject.CreateInstance<FontManagerWindow>();
            try
            {
                typeof(FontManagerWindow).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, profile);
                typeof(FontManagerWindow).GetField("applyTargets", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, new TMP_Text[] { text });
                float before = text.fontSize;
                profile.style.fontSize = 47;
                typeof(FontManagerWindow).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                Undo.FlushUndoRecordObjects();
                Check(text.font == profile.generatedFont && text.fontSize == 47 && text.fontSharedMaterial.GetFloat("_OutlineWidth") == profile.style.outlineWidth, "Apply actual font/style/material to scene text");
                Undo.PerformUndo(); Check(text.fontSize == before, "Undo restores previous text style");
            }
            finally { EditorUtility.ClearProgressBar(); Object.DestroyImmediate(window); }
        }
    }
}
