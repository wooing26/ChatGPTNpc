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
        // 1. API Ŭ���̾�Ʈ �ʱ�ȭ -> ChatGPT ����
        _api = new OpenAIClient(APIKeys.OPENAI_API_KEY);
        _messages = new List<Message>();
    }

    private void Start()
    {
        // CHAT-F
        // C : Context      : ����, ��Ȳ�� ���� �˷����
        // H : Hint         : ���� �亯�� ���� ���
        // A : As A role    : ������ �����ض�
        // T : Target       : �亯�� Ÿ���� �˷����
        // F : Format       : �亯 ���¸� �����ض�

        string systemMessage =
        "����: �ʴ� �ޱ����̴�. " +
        "�丣�ҳ�: ���� ������ �����ִ� �ޱ���ó�� �ൿ�Ѵ�. " +
        "��ǥ: �÷��̾�� ��ȭ�ϸ� ��Ȳ�� �°� ���� ���� ��� ���ɼ��� �˸���, " +
        "���� �� '���� ����'�� ����Ѵ�. " +
        "���� ���� ������ �� �̻� ������� ������ '���ӿ��� �¸�'�� ǥ���Ѵ�. " +
        "ó�� ������ ���� ��Ȳ�� �������� �����Ѵ�. " +
        "ǥ��:  ��ü �亯�� 100�� �̳��� �ۼ��Ѵ�. " +
        "[json ��Ģ] " +
        "ReplyMessage, Appearance, Emotion, StoryImageDescription �ʵ�θ� �����Ѵ�.";

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
        // 0. ������Ʈ (= AI���� ���ϴ� ����� ���� �ؽ�Ʈ>�� �о�´�.
        string prompt = PromptField.text;
        if (string.IsNullOrEmpty(prompt))
        {
            return;
        }
        PromptField.text = string.Empty;

        SendButton.interactable = false;

        // 2. �޽��� �ۼ�
        _messages.Add(new Message(Role.User, prompt));

        GameObject sendSlot = Instantiate(SendTextUI, ContentTransform);
        var SendBubble = sendSlot.GetComponentInChildren<BubbleResizer>();
        if (SendBubble == null)
        {
            return;
        }
        SendBubble.Resize(prompt);

        // 3. �޽��� ������
        var chatRequest = new ChatRequest(_messages, Model.GPT4o);

        // 4. �亯 �ޱ�
        var (npcResponse, response) = await _api.ChatEndpoint.GetCompletionAsync<NpcResponse>(chatRequest);

        // 5. �亯 ����
        var choice = response.FirstChoice;

        // 6. �亯 ���
        GameObject resultSlot = Instantiate(ResultTextUI, ContentTransform);
        var ResultBubble = resultSlot.GetComponentInChildren<BubbleResizer>();
        if (ResultBubble == null)
        {
            return;
        }
        ResultBubble.Resize(npcResponse.ReplyMessage);

        _messages.Add(new Message(Role.Assistant, choice.Message));

        // 8. �亯 ����� ���
        PlayTTS(npcResponse.ReplyMessage);

        // 9. ���丮 �̹��� ����
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
            model: Model.DallE_3    // DALL-E 3 �� ���
            );

        // DALL-E API ȣ��
        var imageResults = await _api.ImagesEndPoint.GenerateImageAsync(request);
        if (imageResults == null || imageResults.Count == 0)
        {
            return;
        }
        GameImage.texture = imageResults[0].Texture;

        SendButton.interactable = true;
    }
}
