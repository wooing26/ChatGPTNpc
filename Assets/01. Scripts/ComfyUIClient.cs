using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;

public class ComfyUIClient : MonoBehaviour
{
    [Header("ComfyUI 설정")]
    [Tooltip("ComfyUI 서버의 URL (예: http://localhost:8188)")]
    public string comfyUIUrl = "http://localhost:8188";

    [Tooltip("Unity의 StreamingAssets 폴더에 있는 워크플로우 JSON 파일 이름")]
    public string workflowFileName = "MegumingLora.json";

    [Tooltip("ComfyUI 서버의 출력 이미지가 저장되는 절대 경로")]
    public string comfyUIOutputFolderPath = @"C:\ComfyUI\ComfyUI_windows_portable\ComfyUI\output\Megumin";

    [Header("디버그")]
    [Tooltip("디버그 로그를 활성화할지 여부")]
    public bool enableDebugLogs = true;

    // MegumingLora.json 워크플로우의 주요 노드 ID 정의
    private const string POSITIVE_PROMPT_NODE_ID = "6";
    private const string NEGATIVE_PROMPT_NODE_ID = "7";
    private const string KSAMPLER_MAIN_NODE_ID = "3";
    private const string KSAMPLER_HIRES_NODE_ID = "22";
    private const string SAVE_IMAGE_GROUP_NODE_ID = "28";

    private const int MAX_WAIT_TIME_SECONDS = 180;
    private const int CHECK_INTERVAL_SECONDS = 3;

    /// <summary>
    /// ComfyUI를 사용하여 이미지를 생성하고 완료될 때까지 기다린 후, 생성된 이미지의 로컬 경로를 반환합니다.
    /// </summary>
    /// <param name="positivePrompt">이미지 생성에 사용할 긍정 프롬프트 텍스트.</param>
    /// <param name="negativePrompt">이미지 생성에 사용할 부정 프롬프트 텍스트 (선택 사항, null이면 기본값 사용).</param>
    /// <param name="onComplete">이미지 생성 완료 시 호출될 콜백. 이미지 경로 또는 null을 전달합니다.</param>
    public IEnumerator GenerateImageAndWait(string positivePrompt, string negativePrompt, Action<string> onComplete)
    {
        if (enableDebugLogs) Debug.Log($"[ComfyUIClient] 🎨 이미지 생성 시작. 긍정: '{positivePrompt}' / 부정: '{negativePrompt}'");

        string promptId = null;

        yield return SendGenerateRequest(positivePrompt, negativePrompt, (id) => {
            promptId = id;
        });

        if (string.IsNullOrEmpty(promptId))
        {
            Debug.LogError("[ComfyUIClient] ❌ 이미지 생성 요청 실패: Prompt ID를 받지 못했습니다.");
            onComplete?.Invoke(null);
            yield break;
        }

        yield return WaitForCompletion(promptId, onComplete);
    }

    private IEnumerator SendGenerateRequest(string positivePrompt, string negativePrompt, Action<string> onComplete)
    {
        string workflowJsonPayload = null;

        try
        {
            workflowJsonPayload = PrepareWorkflow(positivePrompt, negativePrompt);
        }
        catch (FileNotFoundException e)
        {
            Debug.LogError($"[ComfyUIClient] ❌ 워크플로우 파일 오류: {e.Message}");
            onComplete?.Invoke(null);
            yield break;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ComfyUIClient] ❌ 워크플로우 준비 중 오류 발생: {e.Message}");
            onComplete?.Invoke(null);
            yield break;
        }

        using (UnityWebRequest request = new UnityWebRequest($"{comfyUIUrl}/prompt", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(workflowJsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            if (enableDebugLogs) Debug.Log("[ComfyUIClient] ⬆️ 이미지 생성 요청 전송 중...");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    JObject response = JObject.Parse(request.downloadHandler.text);
                    string promptId = response["prompt_id"]?.ToString();

                    if (string.IsNullOrEmpty(promptId))
                    {
                        Debug.LogError("[ComfyUIClient] ❌ 응답에서 prompt_id를 찾을 수 없습니다.");
                        onComplete?.Invoke(null);
                    }
                    else
                    {
                        if (enableDebugLogs) Debug.Log($"[ComfyUIClient] ✅ 이미지 생성 요청 성공. Prompt ID: {promptId}");
                        onComplete?.Invoke(promptId);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ComfyUIClient] ❌ 응답 파싱 실패: {e.Message}\n응답 텍스트: {request.downloadHandler.text}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"[ComfyUIClient] ❌ 이미지 생성 요청 실패: {request.error}\n응답: {request.downloadHandler.text}");
                onComplete?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// Unity StreamingAssets 폴더에서 워크플로우 JSON 파일을 로드하고, 프롬프트와 시드를 업데이트합니다.
    /// </summary>
    /// <param name="positivePrompt">새롭게 설정할 긍정 프롬프트.</param>
    /// <param name="negativePrompt">새롭게 설정할 부정 프롬프트.</param>
    /// <returns>API 요청에 사용될 준비된 JSON 문자열.</returns>
    private string PrepareWorkflow(string positivePrompt, string negativePrompt)
    {
        string workflowPath = Path.Combine(Application.streamingAssetsPath, workflowFileName);

        if (!File.Exists(workflowPath))
        {
            throw new FileNotFoundException($"워크플로우 파일이 StreamingAssets 폴더에 없습니다: {workflowPath}\n" +
                                            $"Unity 프로젝트의 'Assets/StreamingAssets' 폴더에 '{workflowFileName}' 파일을 배치했는지 확인하세요.");
        }

        string rawJson = File.ReadAllText(workflowPath);
        JObject workflow = JObject.Parse(rawJson);

        // 긍정 프롬프트 업데이트 (핵심 변경 사항)
        // 사용자가 입력한 프롬프트 앞에 (megumin:1.1)를 강제로 추가합니다.
        string finalPositivePrompt = "(megumin:1.1), " + positivePrompt;
        UpdatePromptInWorkflow(workflow, POSITIVE_PROMPT_NODE_ID, finalPositivePrompt, "긍정");

        // 부정 프롬프트 업데이트
        UpdatePromptInWorkflow(workflow, NEGATIVE_PROMPT_NODE_ID, negativePrompt, "부정");

        // KSampler 노드 시드 설정 (첫 번째 KSampler)
        UpdateKSamplerSeed(workflow, KSAMPLER_MAIN_NODE_ID, "첫 번째");

        // KSampler 노드 시드 설정 (두 번째 KSampler, hires. fix 등)
        UpdateKSamplerSeed(workflow, KSAMPLER_HIRES_NODE_ID, "두 번째");

        // SaveImage 노드의 파일명 접두사 업데이트
        UpdateSaveImageFilenamePrefix(workflow, SAVE_IMAGE_GROUP_NODE_ID, "Megumin/megumin_unity_");

        JObject apiRequest = new JObject
        {
            ["prompt"] = workflow
        };

        string jsonResult = apiRequest.ToString(Newtonsoft.Json.Formatting.None);

        if (enableDebugLogs)
        {
            string debugPath = Path.Combine(Application.persistentDataPath, $"debug_workflow_prepared_{DateTime.Now.Ticks}.json");
            File.WriteAllText(debugPath, apiRequest.ToString(Newtonsoft.Json.Formatting.Indented));
            Debug.Log($"[ComfyUIClient] 🐛 디버그용 워크플로우 JSON 저장됨: {debugPath}");
        }

        return jsonResult;
    }

    /// <summary>
    /// 특정 CLIPTextEncode 노드의 프롬프트 텍스트를 업데이트합니다.
    /// </summary>
    private void UpdatePromptInWorkflow(JObject workflow, string nodeId, string newPrompt, string promptType)
    {
        JToken promptNode = workflow[nodeId];
        if (promptNode != null && promptNode["class_type"]?.ToString() == "CLIPTextEncode" && promptNode["inputs"]?["text"] != null)
        {
            promptNode["inputs"]["text"] = newPrompt;
            if (enableDebugLogs) Debug.Log($"[ComfyUIClient] 📝 {promptType} 프롬프트 노드 {nodeId} 업데이트: '{newPrompt}'");
        }
        else
        {
            Debug.LogWarning($"[ComfyUIClient] ⚠️ 워크플로우에서 '{promptType}' 프롬프트 노드(ID: {nodeId}) 또는 해당 'text' 입력 필드를 찾을 수 없습니다. 프롬프트가 적용되지 않을 수 있습니다.");
        }
    }

    /// <summary>
    /// 특정 KSampler 노드의 시드를 랜덤 값으로 업데이트합니다.
    /// </summary>
    private void UpdateKSamplerSeed(JObject workflow, string nodeId, string samplerType)
    {
        JToken samplerNode = workflow[nodeId];
        if (samplerNode != null && samplerNode["class_type"]?.ToString() == "KSampler" && samplerNode["inputs"] != null)
        {
            long newSeed = UnityEngine.Random.Range(0, int.MaxValue);
            samplerNode["inputs"]["seed"] = newSeed;

            if (enableDebugLogs) Debug.Log($"[ComfyUIClient] 🎲 {samplerType} KSampler 노드(ID: {nodeId})에 새로운 시드 설정: {newSeed}");
        }
        else
        {
            Debug.LogWarning($"[ComfyUIClient] ⚠️ {samplerType} KSampler 노드(ID: {nodeId}) 또는 해당 시드 입력 필드를 찾을 수 없습니다. 시드가 업데이트되지 않습니다.");
        }
    }

    /// <summary>
    /// SaveImage 노드의 파일명 접두사를 업데이트합니다.
    /// (workflow>이미지 그룹 노드 안에 있는 SaveImage 노드를 찾음)
    /// </summary>
    private void UpdateSaveImageFilenamePrefix(JObject workflow, string groupNodeId, string newPrefix)
    {
        JToken saveImageNode = workflow[groupNodeId];
        if (saveImageNode != null && saveImageNode["widgets_values"] != null && saveImageNode["widgets_values"].Type == JTokenType.Array)
        {
            if (saveImageNode["widgets_values"].HasValues)
            {
                saveImageNode["widgets_values"][0] = newPrefix;
                if (enableDebugLogs) Debug.Log($"[ComfyUIClient] 🖼️ SaveImage 노드의 파일명 접두사 업데이트: '{newPrefix}'");
            }
            else
            {
                Debug.LogWarning($"[ComfyUIClient] ⚠️ SaveImage 노드(ID: {groupNodeId})의 widgets_values 배열이 비어 있습니다. 파일명 접두사를 업데이트할 수 없습니다.");
            }
        }
        else
        {
            Debug.LogWarning($"[ComfyUIClient] ⚠️ 'workflow>이미지' 그룹 노드(ID: {groupNodeId}) 또는 해당 widgets_values를 찾을 수 없습니다. 파일명 접두사를 업데이트할 수 없습니다.");
        }
    }

    private IEnumerator WaitForCompletion(string promptId, Action<string> onComplete)
    {
        int elapsedTime = 0;

        while (elapsedTime < MAX_WAIT_TIME_SECONDS)
        {
            yield return new WaitForSeconds(CHECK_INTERVAL_SECONDS);
            elapsedTime += CHECK_INTERVAL_SECONDS;

            bool isComplete = false;
            string imagePath = null;

            yield return CheckIfComplete(promptId, (complete, path) => {
                isComplete = complete;
                imagePath = path;
            });

            if (isComplete)
            {
                if (enableDebugLogs) Debug.Log($"[ComfyUIClient] 🎉 이미지 생성 완료! 경로: {imagePath}");
                onComplete?.Invoke(imagePath);
                yield break;
            }

            if (enableDebugLogs) Debug.Log($"[ComfyUIClient] ⏳ 이미지 생성 대기 중... ({elapsedTime}/{MAX_WAIT_TIME_SECONDS}초)");
        }

        string latestImage = GetLatestImageFile();
        Debug.LogWarning($"[ComfyUIClient] ⏰ 이미지 생성 타임아웃 ({MAX_WAIT_TIME_SECONDS}초). 최신 파일 반환 시도: {latestImage}");
        onComplete?.Invoke(latestImage);
    }

    private IEnumerator CheckIfComplete(string promptId, Action<bool, string> onComplete)
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{comfyUIUrl}/history/{promptId}"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    JObject history = JObject.Parse(request.downloadHandler.text);

                    if (history.ContainsKey(promptId))
                    {
                        string imagePath = ExtractImagePath(history[promptId], promptId);
                        onComplete?.Invoke(true, imagePath);
                    }
                    else
                    {
                        onComplete?.Invoke(false, null);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ComfyUIClient] ❌ 히스토리 응답 파싱 오류: {e.Message}\n응답 텍스트: {request.downloadHandler.text}");
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning($"[ComfyUIClient] ⚠️ 히스토리 요청 실패 (아직 완료되지 않았을 수 있음): {request.error}");
                onComplete?.Invoke(false, null);
            }
        }
    }

    private string ExtractImagePath(JToken historyEntry, string promptId)
    {
        try
        {
            JToken outputs = historyEntry["outputs"];
            if (outputs != null)
            {
                foreach (JProperty outputNode in outputs)
                {
                    JToken images = outputNode.Value["images"];
                    if (images != null && images.HasValues)
                    {
                        foreach (JToken image in images)
                        {
                            string fileName = image["filename"]?.ToString();
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                string fullPath = Path.Combine(comfyUIOutputFolderPath, fileName);
                                if (File.Exists(fullPath))
                                {
                                    if (enableDebugLogs) Debug.Log($"[ComfyUIClient] 🖼️ 이미지 파일 찾음: {fullPath}");
                                    return fullPath;
                                }
                                else
                                {
                                    if (enableDebugLogs) Debug.LogWarning($"[ComfyUIClient] ⚠️ 히스토리에 언급된 파일이 실제로 존재하지 않음: {fullPath}");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ComfyUIClient] ❌ 이미지 경로 추출 실패 (promptId: {promptId}): {e.Message}");
        }

        return GetLatestImageFile();
    }

    private string GetLatestImageFile()
    {
        try
        {
            if (!Directory.Exists(comfyUIOutputFolderPath))
            {
                Debug.LogError($"[ComfyUIClient] ❌ ComfyUI 출력 폴더가 존재하지 않습니다: {comfyUIOutputFolderPath}. 경로를 올바르게 설정했는지 확인하세요.");
                return null;
            }

            string[] imageFiles = Directory.GetFiles(comfyUIOutputFolderPath, "*.png");

            if (imageFiles.Length == 0)
            {
                Debug.LogWarning("[ComfyUIClient] ⚠️ ComfyUI 출력 폴더에 PNG 이미지 파일이 없습니다.");
                return null;
            }

            string latestFile = null;
            DateTime latestTime = DateTime.MinValue;

            foreach (string file in imageFiles)
            {
                DateTime fileTime = File.GetLastWriteTime(file);
                if (fileTime > latestTime)
                {
                    latestTime = fileTime;
                    latestFile = file;
                }
            }

            if (enableDebugLogs && !string.IsNullOrEmpty(latestFile))
                Debug.Log($"[ComfyUIClient] 🔍 최신 이미지 파일: {latestFile}");

            return latestFile;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ComfyUIClient] ❌ 최신 이미지 파일 찾기 실패: {e.Message}");
            return null;
        }
    }
}