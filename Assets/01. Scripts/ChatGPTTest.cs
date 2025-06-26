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

    // �� �κ��� �߰��մϴ�.
    [Header("ComfyUI ����")]
    public ComfyUIClient ComfyUIClient; // ComfyUIClient ��ũ��Ʈ ����

    [Header("�ʱ� �ó�����")]
    public string[] InitialScenarios = {
        "���� ���忡�� ���� ���� ���� ��",
        "���� �Ա����� ���Ϳ� ��ġ ��",
        "�ձ� ��ȸ�忡�� ���� �ÿ� ��",
        "���� ���� �տ��� ������ ���� �غ� ��"
    };


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

        // ���� �ʱ� �ó����� ����
        string randomScenario = InitialScenarios[Random.Range(0, InitialScenarios.Length)];

        string systemMessage =
            "����: �ʴ� �ޱ����̴�. ȫ���� ������ õ�� ���������� ���� ������ ��ġ�� �ڴ�. " +
            "�丣�ҳ�: ���� ������ �������� �����ϴ� ��2�� �������, �������� �ſ� ���ϴ�. " +
            "����: ��ҿ��� ������ ���������, ������ �������ų� ��2�� ������ ǥ���� ���� " +
            "'�� ����', '����� �ڿ�', '���ĸ����̾߸��� ������ �����̴�' ���� ����� �ݸ��� ����Ѵ�. " +
            "���� ���� �ֹ�: '���溸�� �˰�, ��Һ��� ��ο� ĥ�濡, ���� ��ȫ�� ���̱⸦ �ٶ���. " +
            "������ ���� ������, ������ ��迡 ������ ��ġ��, ������ �ϱ׷����� �Ǿ� ��Ÿ����! �ͽ��÷���!' " +
            "����: ȫ���� Ư���� ��â�� �ڱ�Ұ��� �����ϸ�, �׻� ���� �߿��ϰ� ���� �κ��� �����Ϸ� �Ѵ�. " +
            "��ǥ: �÷��̾���� ��ȭ���� ���� ������ �������� �����ϰ�, ��Ȳ�� ���� ���� ���� ��� ���θ� �����Ѵ�. " +
            $"�ʱ� ��Ȳ: {randomScenario} " +
            "����: �亯�� 100�� �̳��� �ۼ��ϸ�, �ޱ����� Ư¡���� ������ ������ �ݿ��Ѵ�. " +
            "[JSON ��Ģ] ReplyMessage, Appearance, Emotion, ExplosionProbability, StoryImageDescription, GameState �ʵ�θ� �����Ѵ�.";

        _messages.Add(new Message(Role.System, systemMessage));

        CreateMeguminBubble($"�ʱ� �ó����� : {randomScenario}");
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
        // 0. ������Ʈ (= AI���� ���ϴ� ����� ���� �ؽ�Ʈ)�� �о�´�.
        string prompt = PromptField.text;
        if (string.IsNullOrEmpty(prompt))
        {
            return;
        }
        PromptField.text = string.Empty;

        SendButton.interactable = false;

        // 2. �޽��� �ۼ�
        _messages.Add(new Message(Role.User, prompt));
        CreateUserBubble(prompt);
        

        // 3. �޽��� ������
        var chatRequest = new ChatRequest(_messages, Model.GPT4o);

        // 4. �亯 �ޱ�
        var (npcResponse, response) = await _api.ChatEndpoint.GetCompletionAsync<NpcResponse>(chatRequest);

        // 5. �亯 ����
        var choice = response.FirstChoice;

        // 6. �亯 ���
        ProcessMeguminResponse(npcResponse, choice);

        _messages.Add(new Message(Role.Assistant, choice.Message));
    }

    private void ProcessMeguminResponse(NpcResponse response, Choice choice)
    {
        // �ޱ��� ���� ��ǳ�� ����
        string message =
            response.ReplyMessage +
            $"\nExplosionProbability : {response.ExplosionProbability}";

        CreateMeguminBubble(message);

        // ��ȭ ��Ͽ� �߰�
        _messages.Add(new Message(Role.Assistant, choice.Message));

        // 8. �亯 ����� ���
        PlayTTS(response.ReplyMessage);

        // 9. ���丮 �̹��� ����
        if (!string.IsNullOrEmpty(response.StoryImageDescription))
        {
            StartCoroutine(GenerateImageWithComfyUI(response.StoryImageDescription));
        }

        // ���� ���� üũ
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
            Debug.LogError("[ChatGPTTest] ComfyUIClient�� �Ҵ���� �ʾҽ��ϴ�. �̹��� ������ �ǳ�_���ϴ�.");
            SendButton.interactable = true;
            yield break; // �ڷ�ƾ ����
        }

        Debug.Log($"[ChatGPTTest] ComfyUI�� �̹��� ���� ��û: ������Ʈ='{imagePrompt}', ����������Ʈ='{negativePrompt}'");

        // ComfyUIClient�� GenerateImageAndWait �ڷ�ƾ�� �����մϴ�.
        // ��� �̹��� ��δ� onComplete �ݹ��� ���� ���޵˴ϴ�.
        yield return StartCoroutine(ComfyUIClient.GenerateImageAndWait(imagePrompt, negativePrompt, (imagePath) =>
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                Debug.Log($"[ChatGPTTest] ComfyUI �̹��� ���� �Ϸ�. ���: {imagePath}");
                LoadImageTexture(imagePath); // �̹��� �ε� �� ǥ��
            }
            else
            {
                Debug.LogError("[ChatGPTTest] ComfyUI �̹��� ���� ���� �Ǵ� ��θ� ã�� �� �����ϴ�.");
            }
            SendButton.interactable = true; // ��ư �ٽ� Ȱ��ȭ
        }));
    }
    private void LoadImageTexture(string path)
    {
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2); // �̹��� ũ��� �ε� �� �ڵ����� �����˴ϴ�.
            if (texture.LoadImage(fileData))
            {
                if (GameImage != null)
                {
                    GameImage.texture = texture;
                    GameImage.SetNativeSize(); // RawImage ũ�⸦ �̹��� ���� ũ�⿡ ���� (���� ����)
                    Debug.Log($"[ChatGPTTest] �̹��� Texture2D �ε� ����: {path}");
                }
                else
                {
                    Debug.LogWarning("[ChatGPTTest] GameImage RawImage ������Ʈ�� �Ҵ���� �ʾҽ��ϴ�.");
                }
            }
            else
            {
                Debug.LogError($"[ChatGPTTest] �̹��� ������ �ε� ����: {path}");
            }
        }
        else
        {
            Debug.LogError($"[ChatGPTTest] ������ �������� �ʽ��ϴ�: {path}");
        }
    }
}
