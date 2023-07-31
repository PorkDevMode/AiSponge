using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Cinemachine;
using DialogueAI;
using Newtonsoft.Json;
using UnityEngine;
using Random = System.Random;
using OpenAI;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Net;

#pragma warning disable CS4014

public class AIThing : MonoBehaviour
{
    private Random _random = new Random();

    //should probably change this to a safer method of storing the keys
    [SerializeField] private string openAIKey;
    [SerializeField] private string fakeYouUsernameOrEMail;
    [SerializeField] private string fakeYouPassword;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private TextMeshProUGUI topicText;
    [SerializeField] public AudioClip[] audioClips; // Put in here for a character like gary that does not have a voicemodel and speaks gibberish
    [SerializeField] private List<string> proxies;
    private int currentProxyIndex = 0;



    private string GetNextProxy()
    {
        if (currentProxyIndex >= proxies.Count)
            currentProxyIndex = 0;

        return proxies[currentProxyIndex++];
    }

    // 3. Configure HttpClient to use the selected proxy:
    private HttpClient GetHttpClientWithProxy()
    {
        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(GetNextProxy())
        };

        return new HttpClient(handler);
    }
    [SerializeField] private CinemachineVirtualCamera _cinemachineVirtualCamera;
    [SerializeField] private TextMeshProUGUI subtitles;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private List<AudioClip> _clips;
    private HttpClient _client = new();
    private OpenAIApi _openAI;

    public VideoClip clipToPlay;

    IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    void Start()
    {
        _openAI = new OpenAIApi(openAIKey);
        Init();
    }

    async void Init()
    {
        if (!File.Exists($"{Environment.CurrentDirectory}\\Assets\\Scripts\\key.txt"))
            File.WriteAllText($"{Environment.CurrentDirectory}\\Assets\\Scripts\\key.txt", "");

        string cookie = File.ReadAllText($"{Environment.CurrentDirectory}\\Assets\\Scripts\\key.txt");

        if (cookie == "")
        {
            var obj = new
            {
                username_or_email = fakeYouUsernameOrEMail,
                password = fakeYouPassword
            };

            var response = await _client.PostAsync("https://api.fakeyou.com/login",
                new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json"));
            var d = JsonConvert.SerializeObject(response.Headers.GetValues("set-cookie").First());
            var l = d.Split(';');
            Debug.Log(d);
            cookie = l[0].Replace("session=", "");
            cookie = cookie.Replace("\"", "");
            File.WriteAllText($"{Environment.CurrentDirectory}\\Assets\\Scripts\\key.txt", cookie);
        }

        _client.DefaultRequestHeaders.Add("Authorization", cookie);
        _client.DefaultRequestHeaders.Add("Accept", "application/json");

        // Read the blacklist
        List<string> blacklist = new List<string>();
        string blacklistPath = $"{Environment.CurrentDirectory}\\Assets\\Scripts\\blacklist.json";
        if (File.Exists(blacklistPath))
        {
            blacklist = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(blacklistPath));
        }

        // Pick a random topic
        List<string> topics =
            JsonConvert.DeserializeObject<List<string>>(
                File.ReadAllText($"{Environment.CurrentDirectory}\\Assets\\Scripts\\topics.json"));

        // If there are no topics, play a video clip and restart in 10 seconds
        if (topics.Count == 0)
        {
            videoPlayer.clip = clipToPlay;
            videoPlayer.Play();

            StartCoroutine(LoadSceneAfterDelay("1.0.1", 5.0f));
        }

        string topic = topics[_random.Next(0, topics.Count)];

        if (topicText != null)
        {
            topicText.text = "Next Topic: " + topic;
        }
        // Add the chosen topic to the blacklist and write it back to the file
        if (!blacklist.Contains(topic))
        {
            blacklist.Add(topic);
            File.WriteAllText(blacklistPath, JsonConvert.SerializeObject(blacklist));
        }

        //play the timecards/intro
        if (_clips.Count > 0)
        {
            audioSource.clip = _clips[_random.Next(0, _clips.Count)];
            audioSource.Play();
            StartCoroutine(waitForTransition(topic));
        }
        else
        {
            Generate(topic);
        }
    }

    private IEnumerator waitForTransition(string topic)
    {
        while (videoPlayer.isPlaying)
        {
            yield return null;
        }

        Generate(topic);
    }

    IEnumerator RetryGenerateAfterDelay(string topic)
    {
        yield return new WaitForSeconds(15);
        Generate(topic);
    }

    async void Generate(string topic)
    {
        string[] text = new[] { "" };
        if (File.Exists("Assets/Scripts/Next.txt"))
            text = File.ReadAllLines("Assets/Scripts/Next.txt");

        //delete the script from the file so you don't get the same script twice
        File.WriteAllText("Assets/Scripts/Next.txt", "");
        List<Dialogue> dialogues = new List<Dialogue>();

        if (text.Length == 0)
        {
            await GenerateNext(topic);
            text = File.ReadAllLines("Assets/Scripts/Next.txt");
            List<string> topics =
                JsonConvert.DeserializeObject<List<string>>(
                    File.ReadAllText($"{Environment.CurrentDirectory}\\Assets\\Scripts\\topics.json"));
            string t = topics[_random.Next(0, topics.Count)];
            GenerateNext(t);
        }
        else
        {
            GenerateNext(topic);
        }

        List<Task> ttsTasks = new List<Task>();

        foreach (var line in text)
        {
            string voicemodelUuid = "";
            string textToSay = "";
            string character = "";

            // if line starts with character here
            if (line.StartsWith("SpongeBob:"))
            {
                textToSay = line.Replace("SpongeBob:", "");
                voicemodelUuid = "TM:618j8qwddnsn";
                character = "spongebob";
            }
            else if (line.StartsWith("Spongebob:"))
            {
                textToSay = line.Replace("Spongebob:", "");
                voicemodelUuid = "TM:618j8qwddnsn";
                character = "spongebob";
            }
            else if (line.StartsWith("Patrick:"))
            {
                textToSay = line.Replace("Patrick:", "");
                voicemodelUuid = "TM:ptcaavcfhwxd";
                character = "patrick";
            }
            else if (line.StartsWith("Mr. Krabs"))
            {
                voicemodelUuid = "TM:ade4ta7rc720";
                textToSay = line.Replace("Mr. Krabs", "");
                character = "mrkrabs";
            }
            else if (line.StartsWith("Squidward:"))
            {
                voicemodelUuid = "TM:4e2xqpwqaggr";
                textToSay = line.Replace("Squidward:", "").ToUpper(); //converting to caps because funny loudward
                character = "squidward";
            }
            else if (line.StartsWith("Sandy:"))
            {
                voicemodelUuid = "TM:eaachm5yecgz";
                textToSay = line.Replace("Sandy:", "");
                character = "sandy";
            }
            else if (line.StartsWith("Gary:"))
            {
                voicemodelUuid = "TM:eaachm5yecgz";
                textToSay = line.Replace("Gary:", "");
                character = "gary";
            }

            if (textToSay == "")
                continue;

            ttsTasks.Add(CreateTTSRequest(textToSay, voicemodelUuid, dialogues, character));
        }

        await Task.WhenAll(ttsTasks);

        StartCoroutine(Speak(dialogues));
    }

    private async Task CreateTTSRequest(string textToSay, string voicemodelUuid, List<Dialogue> dialogues, string character)
    {
        var jsonObj = new
        {
            inference_text = textToSay,
            tts_model_token = voicemodelUuid,
            uuid_idempotency_token = Guid.NewGuid().ToString()
        };
        var content = new StringContent(JsonConvert.SerializeObject(jsonObj), Encoding.UTF8, "application/json");

        bool retry = true;
        while (retry)
        {
            var response2 = await _client.PostAsync("https://api.fakeyou.com/tts/inference", content);
            var responseString = await response2.Content.ReadAsStringAsync();
            SpeakResponse speakResponse = null;
            try
            {
                speakResponse = JsonConvert.DeserializeObject<SpeakResponse>(responseString);
            }
            catch (JsonReaderException)
            {
                Debug.Log("Error parsing API response. Probably due to rate limiting. Waiting 10 seconds before retrying.");
                await Task.Delay(10000);
                continue;
            }

            if (!speakResponse.success)
            {
                continue;
            }

            retry = false;

            dialogues.Add(new Dialogue
            {
                uuid = speakResponse.inference_job_token,
                text = textToSay,
                character = character
            });
            Debug.Log(responseString);
            await Task.Delay(5500); // for rate limiting. rate limit is so fucking annoying that you get limited even with 3 second delay
        }
    }

    private async Task GenerateNext(string topic)
    {
        var request = new CreateCompletionRequest
        {
            Model = "text-davinci-002",
            Prompt =
                $"Create a script for a scene from Spongebob where characters discuss a topic. Possible Characters Include Spongebob, Patrick, Squidward, Sandy, Mr. Krabs and very rarely Gary. Use the format: Character: <dialogue>. Only reply with coherent character dialogue. Around 12 - 15 lines of dialogue with talking only and make sure that one character does not talk more than once in row. The topic is: {topic}",
            MaxTokens = 700
        };
        var response = await _openAI.CreateCompletion(request);
        if (response.Error != null || response.Choices == null)
        {
            GenerateNext(topic);
        }
        else
        {
            var text = response.Choices[0].Text;
            File.WriteAllText("Assets/Scripts/Next.txt", text);

            Debug.Log("GPT Response:\n" + text);
        }
    }

    private IEnumerator Speak(List<Dialogue> l)
    {
        videoPlayer.gameObject.SetActive(false);
        foreach (var dialogue in l)
        {
            yield return Speak(dialogue);
        }

        while (File.ReadAllText("Assets/Scripts/Next.txt") == "")
        {
            yield return null;
        }

        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    private IEnumerator CreateNewVoiceRequest(Dialogue d, Action<string> callback)
    {
        var jsonObj = new
        {
            tts_model_token = d.model,
            uuid_idempotency_token = Guid.NewGuid().ToString(),
            inference_text = d.text,
        };

        var content = new StringContent(JsonConvert.SerializeObject(jsonObj), Encoding.UTF8, "application/json");
        var response = _client.PostAsync("https://api.fakeyou.com/tts/inference", content).Result;

        if (response.IsSuccessStatusCode)
        {
            var responseString = response.Content.ReadAsStringAsync().Result;
            var speakResponse = JsonConvert.DeserializeObject<SpeakResponse>(responseString);

            callback(speakResponse.inference_job_token);
        }
        else
        {
            Debug.LogError("Error in FakeYou API request: " + response.StatusCode);
            callback(null);
        }
        yield return null;
    }

    public GameObject[] gt;

    private IEnumerator TurnToSpeaker(Transform objectTransform, Transform speakerTransform)
    {
        Vector3 direction = (speakerTransform.position - objectTransform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            while (Quaternion.Angle(objectTransform.rotation, targetRotation) > 0.05f)
            {
                objectTransform.rotation = Quaternion.Slerp(objectTransform.rotation, targetRotation, Time.deltaTime * 2.0f);
                yield return null;
            }
        }
    }

    private IEnumerator Speak(Dialogue d)
    {
        var content = _client.GetAsync($"https://api.fakeyou.com/tts/job/{d.uuid}").Result.Content;
        Debug.Log(content.ReadAsStringAsync().Result);
        var v = JsonConvert.DeserializeObject<GetResponse>(content.ReadAsStringAsync().Result);
        if (v.state == null || v.state.status == "pending" || v.state.status == "started" || v.state.status == "attempt_failed")
        {
            yield return new WaitForSeconds(1.5f);
            yield return Speak(d);
        }
        else if (v.state.status == "complete_success")
        {
            if (GameObject.Find(d.character) != null && _cinemachineVirtualCamera != null)
            {
                GameObject character = GameObject.Find(d.character);
                Transform t = GameObject.Find(d.character).transform;
                _cinemachineVirtualCamera.LookAt = t;
                _cinemachineVirtualCamera.Follow = t;
                yield return new WaitForSeconds(1);

                if (gt.Length > 0)
                {
                    foreach (GameObject obj in gt)
                    {
                        if (obj != null)
                        {
                            StartCoroutine(TurnToSpeaker(obj.transform, t));
                        }
                    }
                }
            }

            if (subtitles != null)
                subtitles.text = d.text;

            using (var uwr = UnityWebRequestMultimedia.GetAudioClip($"https://storage.googleapis.com/vocodes-public{v.state.maybe_public_bucket_wav_audio_path}",
                AudioType.WAV))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.Log(uwr.error);
                }
                else
                {
                    audioSource.clip = DownloadHandlerAudioClip.GetContent(uwr);

                    if (d.character == "squidward")
                    {
                        float[] clipData = new float[audioSource.clip.samples * audioSource.clip.channels];
                        audioSource.clip.GetData(clipData, 0);
                        for (int i = 0; i < clipData.Length; i++)
                        {
                            clipData[i] *= 1.5f;
                        }

                        audioSource.clip.SetData(clipData, 0);
                    }
                    else if (d.character == "gary")
                    {
                        audioSource = GetComponent<AudioSource>();
                        audioSource.clip = audioClips[0];

                        audioSource.Play();

                        audioSource.Stop();
                    }

                    GameObject character = GameObject.Find(d.character);
                    if (character != null)
                    {
                        Animator characterAnimator = character.GetComponent<Animator>();
                        if (characterAnimator != null)
                        {
                            characterAnimator.SetBool("isSpeaking", true);
                        }
                    }

                    audioSource.Play();

                    yield return new WaitForSeconds(audioSource.clip.length);

                    if (character != null)
                    {
                        Animator characterAnimator = character.GetComponent<Animator>();
                        if (characterAnimator != null)
                        {
                            characterAnimator.SetBool("isSpeaking", false);
                        }
                    }

                    while (audioSource.isPlaying)
                        yield return null;
                }
            }
        }
        else
        {
            string newUuid = null;
            yield return CreateNewVoiceRequest(d, result => { newUuid = result; });

            if (!string.IsNullOrEmpty(newUuid))
            {
                d.uuid = newUuid;
                yield return Speak(d);
            }
            else
            {
                Debug.LogError("Failed to create new voice request");
            }
        }
    }
}
