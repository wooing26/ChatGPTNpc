using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using OpenAI.Chat;
using OpenAI.Models;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Images;

public class ChatGPTTest : MonoBehaviour
{
    public Transform      ContentTransform;
    public GameObject     ResultTextUI;
    public GameObject     SendTextUI;
    public TMP_InputField PromptField;
    public Button         SendButton;
    public AudioSource    MyAudioSource;
    public RawImage       GameImage;


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

        string systemMessage =
        "역할: 너는 메구밍이다. " +
        "페르소나: 폭발 마법에 미쳐있는 메구밍처럼 행동한다. " +
        "목표: 플레이어와 대화하며 상황에 맞게 폭발 마법 사용 가능성을 알리고, " +
        "폭발 시 '게임 오버'를 출력한다. " +
        "만약 폭발 마법을 더 이상 사용하지 않으면 '게임에서 승리'를 표시한다. " +
        "처음 시작할 때는 상황을 무작위로 선택한다. " +
        "표현:  전체 답변은 100자 이내로 작성한다. " +
        "[json 규칙] " +
        "ReplyMessage, Appearance, Emotion, StoryImageDescription 필드로만 응답한다.";

        _messages.Add(new Message(Role.System, systemMessage));
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
        // 0. 프롬포트 (= AI에게 원하는 명령을 적은 텍스트>를 읽어온다.
        string prompt = PromptField.text;
        if (string.IsNullOrEmpty(prompt))
        {
            return;
        }
        PromptField.text = string.Empty;

        SendButton.interactable = false;

        // 2. 메시지 작성
        _messages.Add(new Message(Role.User, prompt));

        GameObject sendSlot = Instantiate(SendTextUI, ContentTransform);
        var SendBubble = sendSlot.GetComponentInChildren<BubbleResizer>();
        if (SendBubble == null)
        {
            return;
        }
        SendBubble.Resize(prompt);

        // 3. 메시지 보내기
        var chatRequest = new ChatRequest(_messages, Model.GPT4o);

        // 4. 답변 받기
        var (npcResponse, response) = await _api.ChatEndpoint.GetCompletionAsync<NpcResponse>(chatRequest);

        // 5. 답변 선택
        var choice = response.FirstChoice;

        // 6. 답변 출력
        GameObject resultSlot = Instantiate(ResultTextUI, ContentTransform);
        var ResultBubble = resultSlot.GetComponentInChildren<BubbleResizer>();
        if (ResultBubble == null)
        {
            return;
        }
        ResultBubble.Resize(npcResponse.ReplyMessage);

        _messages.Add(new Message(Role.Assistant, choice.Message));

        // 8. 답변 오디오 재생
        PlayTTS(npcResponse.ReplyMessage);

        // 9. 스토리 이미지 생성
        //GenerateImage(npcResponse.StoryImageDescription);
    }

    private async void PlayTTS(string text)
    {
        var request = new SpeechRequest(text);
        var speechClip = await _api.AudioEndpoint.GetSpeechAsync(request);
        MyAudioSource.PlayOneShot(speechClip);
    }

    private async void GenerateImage(string imagePrompt)
    {
        var request = new ImageGenerationRequest(
            prompt: imagePrompt,
            model: Model.DallE_3    // DALL-E 3 모델 사용
            );

        // DALL-E API 호출
        var imageResults = await _api.ImagesEndPoint.GenerateImageAsync(request);
        if (imageResults == null || imageResults.Count == 0)
        {
            return;
        }
        GameImage.texture = imageResults[0].Texture;

        SendButton.interactable = true;
    }
}
