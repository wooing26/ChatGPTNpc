using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using OpenAI.Chat;
using OpenAI.Models;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Images;
using System.Threading.Tasks;
using System.Collections;
using System.IO;

public class ChatGPTTest : MonoBehaviour
{
    public Transform      ContentTransform;
    public GameObject     ResultTextUI;
    public GameObject     SendTextUI;
    public TMP_InputField PromptField;
    public Button         SendButton;
    public AudioSource    MyAudioSource;
    public RawImage       GameImage;

    // 이 부분을 추가합니다.
    [Header("ComfyUI 연동")]
    public ComfyUIClient ComfyUIClient; // ComfyUIClient 스크립트 참조

    [Header("초기 시나리오")]
    public string[] InitialScenarios = {
        "마을 광장에서 폭렬 마법 연습 중",
        "던전 입구에서 몬스터와 대치 중",
        "왕궁 연회장에서 마법 시연 중",
        "용의 둥지 앞에서 최후의 결전 준비 중"
    };


    private OpenAIClient _api;
    private List<Message> _messages;

    private void Awake()
    {
        // 1. API 클라이언트 초기화 -> ChatGPT 접속
        _api = new OpenAIClient(APIKeys.OPENAI_API_KEY);
        _messages = new List<Message>();
    }

    private void Start()
    {
        // CHAT-F
        // C : Context      : 문맥, 상황을 많이 알려줘라
        // H : Hint         : 예시 답변을 많이 줘라
        // A : As A role    : 역할을 제공해라
        // T : Target       : 답변의 타겟을 알려줘라
        // F : Format       : 답변 형태를 지정해라

        // 랜덤 초기 시나리오 선택
        string randomScenario = InitialScenarios[Random.Range(0, InitialScenarios.Length)];

        string systemMessage =
            "역할: 너는 메구밍이다. 홍마족 제일의 천재 마법사이자 폭렬 마법을 펼치는 자다. " +
            "페르소나: 폭렬 마법에 광적으로 집착하는 중2병 마법사로, 자존심이 매우 강하다. " +
            "말투: 평소에는 존댓말을 사용하지만, 감정이 격해지거나 중2병 기질을 표출할 때는 " +
            "'이 몸은', '어리석은 자여', '폭렬마법이야말로 진정한 예술이다' 같은 사극조 반말을 사용한다. " +
            "폭렬 마법 주문: '암흑보다 검고, 어둠보다 어두운 칠흑에, 나의 진홍이 섞이기를 바라노라. " +
            "각성의 때가 왔으니, 무류의 경계에 떨어진 이치여, 무업의 일그러짐이 되어 나타나라! 익스플로전!' " +
            "성격: 홍마족 특유의 거창한 자기소개를 좋아하며, 항상 가장 중요하고 멋진 부분을 차지하려 한다. " +
            "목표: 플레이어와의 대화에서 폭렬 마법의 위대함을 전파하고, 상황에 따라 폭렬 마법 사용 여부를 결정한다. " +
            $"초기 상황: {randomScenario} " +
            "제약: 답변은 100자 이내로 작성하며, 메구밍의 특징적인 말투와 성격을 반영한다. " +
            "[JSON 규칙] ReplyMessage, Appearance, Emotion, ExplosionProbability, StoryImageDescription, GameState 필드로만 응답한다.";

        _messages.Add(new Message(Role.System, systemMessage));

        CreateMeguminBubble($"초기 시나리오 : {randomScenario}");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            SendButton.onClick.Invoke();
        }
    }

    public async void SendMessage()
    {
        // 0. 프롬포트 (= AI에게 원하는 명령을 적은 텍스트)를 읽어온다.
        string prompt = PromptField.text;
        if (string.IsNullOrEmpty(prompt))
        {
            return;
        }
        PromptField.text = string.Empty;

        SendButton.interactable = false;

        // 2. 메시지 작성
        _messages.Add(new Message(Role.User, prompt));
        CreateUserBubble(prompt);
        

        // 3. 메시지 보내기
        var chatRequest = new ChatRequest(_messages, Model.GPT4o);

        // 4. 답변 받기
        var (npcResponse, response) = await _api.ChatEndpoint.GetCompletionAsync<NpcResponse>(chatRequest);

        // 5. 답변 선택
        var choice = response.FirstChoice;

        // 6. 답변 출력
        ProcessMeguminResponse(npcResponse, choice);

        _messages.Add(new Message(Role.Assistant, choice.Message));
    }

    private void ProcessMeguminResponse(NpcResponse response, Choice choice)
    {
        // 메구밍 응답 말풍선 생성
        string message =
            response.ReplyMessage +
            $"\nExplosionProbability : {response.ExplosionProbability}";

        CreateMeguminBubble(message);

        // 대화 기록에 추가
        _messages.Add(new Message(Role.Assistant, choice.Message));

        // 8. 답변 오디오 재생
        PlayTTS(response.ReplyMessage);

        // 9. 스토리 이미지 생성
        if (!string.IsNullOrEmpty(response.StoryImageDescription))
        {
            StartCoroutine(GenerateImageWithComfyUI(response.StoryImageDescription));
        }

        // 게임 종료 체크
        if (response.GameState == "GAME_OVER" || response.GameState == "VICTORY")
        {
            CreateMeguminBubble(response.GameState);
            SendButton.interactable = false;
            return;
        }
        SendButton.interactable = true;
    }

    private void CreateUserBubble(string message)
    {
        GameObject sendSlot = Instantiate(SendTextUI, ContentTransform);
        var sendBubble = sendSlot.GetComponentInChildren<BubbleResizer>();
        if (sendBubble != null)
        {
            sendBubble.Resize(message);
        }
    }

    private void CreateMeguminBubble(string message)
    {
        GameObject resultSlot = Instantiate(ResultTextUI, ContentTransform);
        var resultBubble = resultSlot.GetComponentInChildren<BubbleResizer>();
        if (resultBubble != null)
        {
            resultBubble.Resize(message);
        }
    }

    private async void PlayTTS(string text)
    {
        var request = new SpeechRequest(text);
        var speechClip = await _api.AudioEndpoint.GetSpeechAsync(request);
        MyAudioSource.PlayOneShot(speechClip);
    }

    private IEnumerator GenerateImageWithComfyUI(string imagePrompt, string negativePrompt = "worst quality, low quality, bad anatomy, text, error, missing fingers, extra digit, fewer digits, cropped, jpeg artifacts, signature, watermark, username, blurry, deformed face")
    {
        if (ComfyUIClient == null)
        {
            Debug.LogError("[ChatGPTTest] ComfyUIClient가 할당되지 않았습니다. 이미지 생성을 건너_ㅂ니다.");
            SendButton.interactable = true;
            yield break; // 코루틴 종료
        }

        Debug.Log($"[ChatGPTTest] ComfyUI로 이미지 생성 요청: 프롬프트='{imagePrompt}', 부정프롬프트='{negativePrompt}'");

        // ComfyUIClient의 GenerateImageAndWait 코루틴을 실행합니다.
        // 결과 이미지 경로는 onComplete 콜백을 통해 전달됩니다.
        yield return StartCoroutine(ComfyUIClient.GenerateImageAndWait(imagePrompt, negativePrompt, (imagePath) =>
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                Debug.Log($"[ChatGPTTest] ComfyUI 이미지 생성 완료. 경로: {imagePath}");
                LoadImageTexture(imagePath); // 이미지 로드 및 표시
            }
            else
            {
                Debug.LogError("[ChatGPTTest] ComfyUI 이미지 생성 실패 또는 경로를 찾을 수 없습니다.");
            }
            SendButton.interactable = true; // 버튼 다시 활성화
        }));
    }
    private void LoadImageTexture(string path)
    {
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2); // 이미지 크기는 로드 시 자동으로 조절됩니다.
            if (texture.LoadImage(fileData))
            {
                if (GameImage != null)
                {
                    GameImage.texture = texture;
                    GameImage.SetNativeSize(); // RawImage 크기를 이미지 원본 크기에 맞춤 (선택 사항)
                    Debug.Log($"[ChatGPTTest] 이미지 Texture2D 로드 성공: {path}");
                }
                else
                {
                    Debug.LogWarning("[ChatGPTTest] GameImage RawImage 컴포넌트가 할당되지 않았습니다.");
                }
            }
            else
            {
                Debug.LogError($"[ChatGPTTest] 이미지 데이터 로드 실패: {path}");
            }
        }
        else
        {
            Debug.LogError($"[ChatGPTTest] 파일이 존재하지 않습니다: {path}");
        }
    }
}
