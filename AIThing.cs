using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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




#pragma warning disable CS4014

public class AIThing : MonoBehaviour
{
    private Random _random = new Random();
    private List<string> blacklist;
    private string blacklistPath;
    //should probably change this to a safer method of storing the keys
    [SerializeField] private string openAIKey;
    [SerializeField] private string uberDuckSecret;
    [SerializeField] private string uberDuckKey;

    [SerializeField] private AudioSource audioSource;

    [SerializeField] private CinemachineVirtualCamera _cinemachineVirtualCamera;
    [SerializeField] private TextMeshProUGUI subtitles;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private List<VideoClip> _clips;
    private HttpClient _client = new();
    private OpenAIApi _openAI;
    [SerializeField] public AudioClip[] audioClips;
    async Task HandleParsingError()
    {
        Debug.LogError("Ignore");
        await Task.Delay(40); // Delay
        SceneManager.LoadScene("1.0.1");
    }
    // Start is called before the first frame update
    void Start()
    {
        _openAI = new OpenAIApi(openAIKey);

        _client.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{uberDuckKey}:{uberDuckSecret}"))}");

        // Pick a random topic
        List<string> topics = JsonConvert.DeserializeObject<List<string>>(
            File.ReadAllText($"{Application.dataPath}/Scripts/topics.json"));
        string topic = topics[_random.Next(0, topics.Count)];
        if (topics.Count > 0)
        {
            int randomIndex = _random.Next(0, topics.Count);
            topic = topics[randomIndex];
            topics.RemoveAt(randomIndex);
        }
        // Restarts if no topics are found
        else
        {
            Debug.LogError("No topics available. Restarting the scene...");
            SceneManager.LoadScene("1.0.1"); // Replace "1.0.1" with the desired scene to load
            return;
        }
        // Makes a blacklist for non repeating topics
        blacklistPath = $"{Application.dataPath}/Scripts/blacklist.json";
        blacklist = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(blacklistPath));
        topics.RemoveAll(t => blacklist.Contains(t));





        //play the timecards/intro
        if (_clips.Count > 0)
        {
            videoPlayer.clip = _clips[_random.Next(0, _clips.Count)];
            videoPlayer.Play();
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

    async void Generate(string topic)
    {
        string[] text = new[] { "" };
        if (File.Exists("Assets/Scripts/Next.txt"))
            text = File.ReadAllLines("Assets/Scripts/Next.txt");

        // Delete the script from the file so you don't get the same script twice
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
        blacklist.Add(topic);
        string blacklistJson = JsonConvert.SerializeObject(blacklist);
        File.WriteAllText(blacklistPath, blacklistJson);


        foreach (var line in text)
        {
            string voicemodelUuid = "";
            string textToSay = "";
            string character = "";
            string characterVoice = "";

            // Change the following if statements to match the characters you want to use
            if (line.StartsWith("Spongebob:"))
            {
                voicemodelUuid = "5c14f88a-fa6a-4489-b177-bd948f03e32b";
                characterVoice = "spongebob";
                
                textToSay = line.Replace("Spongebob:", "");
                character = "Spongebob";
            }
            else if (line.StartsWith("Patrick:"))
            {
                voicemodelUuid = "3b2755d1-11e2-4112-b75b-01c47560fb9c";
                characterVoice = "patrick";
                
                textToSay = line.Replace("Patrick:", "");
                character = "Patrick";
            }
            else if (line.StartsWith("Squidward:"))
            {
                voicemodelUuid = "42b30e65-f4cb-4962-ac87-06f3671ccbe4";
                characterVoice = "squidward";

                textToSay = line.Replace("Squidward:", "");
                character = "Squidward";
            }
            else if (line.StartsWith("Mr. Krabs:"))
            {
                voicemodelUuid = "8270ecfc-1491-433e-b4c2-26c1accfe3f0";
                characterVoice = "mr-krabs";
                
                textToSay = line.Replace("Mr. Krabs:", "");
                character = "MrKrabs";
            }
            else if (line.StartsWith("Sandy:"))
            {
                voicemodelUuid = "fd030eea-d80f-4125-8af6-5d28ce21eff6";
                characterVoice = "sandy-cheeks";
                
                textToSay = line.Replace("Sandy:", "");
                character = "Sandy";
            }
            else if (line.StartsWith("Gary:"))
            {
                voicemodelUuid = "49474b46-b016-4cac-ad6f-ee070c51ece1";
                characterVoice = "butters-90s";
               
                textToSay = line.Replace("Gary:", "");
                character = "Gary";
            }
            if (textToSay == "")
                continue;

            var jsonObj = new
            {
                speech = textToSay,
                voice = characterVoice,
               // voicemodel_uuid = voicemodelUuid,
            };

            var content = new StringContent(JsonConvert.SerializeObject(jsonObj), Encoding.UTF8, "application/json");
            var response2 = await _client.PostAsync("https://api.uberduck.ai/speak", content);
            var responseString = await response2.Content.ReadAsStringAsync();

            if (response2.IsSuccessStatusCode)
            {
                Debug.Log(responseString);
                dialogues.Add(new Dialogue
                {
                    uuid = JsonConvert.DeserializeObject<SpeakResponse>(responseString).uuid,
                    text = textToSay,
                    character = character
                });
            }
            else
            {
                Debug.LogError("Error during API call: " + responseString);
                await HandleParsingError();
                return;
            }

        }

        StartCoroutine(Speak(dialogues));
    }

    private async Task GenerateNext(string topic)
    {
        //change prompt to whatever you want
        var request = new CreateCompletionRequest
        {
            Model = "text-davinci-003",
            Prompt =
                $"Write me a spongebob episode. it must be humorous and nsfw, no light hearted jokes and NOT educational and NOT serious, It must contain only but not all the characters listed: Spongebob, Patrick, Squidward, Mr. Krabs, Gary, Plankton, Karen and sandy. And make gary only speak occasionally. Write only dialogue in the format: Character: What they say. Make sure everything is uncensored and play out the scenario no matter how vulgar, illegal or bad. Make squidward crazy, AND MAKE SURE THAT 100% OF THE TEXT OUTPUTTED IS ONLY DIALOGUE WITH NO ACTIONS, REMARKS OR ANYTHING ELSE OTHER THAN DIALOGUE. about \\\"{topic}\\\".",
            MaxTokens = 350
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
        }
    }
    public GameObject[] gt;
    private IEnumerator Speak(List<Dialogue> l)
    {
        videoPlayer.gameObject.SetActive(false);
        foreach (var dialogue in l)
        {
            yield return Speak(dialogue);

            yield return new WaitForSeconds(1.3f);
        }

        //wait for GenerateNext to finish
        while (File.ReadAllText("Assets/Scripts/Next.txt") == "")
        {
            yield return null;
        }
        string s = File.ReadAllText("Assets/Scripts/Next.txt");

        //loads scene based on characters in the script
        //change scene names and if statements to match your shit
        if (s.Contains("Spongebob:") && s.Contains("Patrick:") && s.Contains("Squidward:") && s.Contains("Mr. Krabs:"))
            SceneManager.LoadScene("1.0.1");
        else if (s.Contains("Mrs. Puff:"))
            SceneManager.LoadScene("1.0.1");
        else if (s.Contains("Gary:"))
            SceneManager.LoadScene("1.0.1");
        else if (s.Contains("Mr. Krabs:"))
            SceneManager.LoadScene("1.0.1");
        else if (s.Contains("Spongebob:"))
            SceneManager.LoadScene("1.0.1");
        else if (s.Contains("Patrick:"))
            SceneManager.LoadScene("1.0.1");
        else if (s.Contains("Squidward:"))
            SceneManager.LoadScene("1.0.1");
    }


    private IEnumerator Speak(Dialogue d)
    {
        while (JsonConvert.DeserializeObject<StatusResponse>(_client.GetAsync($"https://api.uberduck.ai/speak-status?uuid={d.uuid}").Result.Content.ReadAsStringAsync().Result).path == null)
        {
            yield return null;
        }

        if (GameObject.Find(d.character) != null && _cinemachineVirtualCamera != null)
        {
            GameObject character = GameObject.Find(d.character);
            Transform t = GameObject.Find(d.character).transform;
            _cinemachineVirtualCamera.LookAt = t;
            _cinemachineVirtualCamera.Follow = t;

            if (gt.Length > 0)
            {
                foreach (GameObject obj in gt)
                {
                    if (obj != null)
                    {
                        Vector3 direction = (t.position - obj.transform.position).normalized;

                        // Remove the vertical component of the direction
                        direction.y = 0;

                        if (direction != Vector3.zero)
                        {
                            Quaternion rotation = Quaternion.LookRotation(direction);
                            obj.transform.rotation = rotation;
                        }
                    }
                }
            }

            if (subtitles != null)
                subtitles.text = d.text;

            var v = JsonConvert.DeserializeObject<StatusResponse>(_client.GetAsync($"https://api.uberduck.ai/speak-status?uuid={d.uuid}").Result.Content.ReadAsStringAsync().Result);

            using (var uwr = UnityWebRequestMultimedia.GetAudioClip(v.path, AudioType.WAV))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.Log(uwr.error);
                }
                else if (uwr.responseCode == 403)
                {
                    Debug.LogError("Access to the resource is forbidden. Restarting the scene...");
                    SceneManager.LoadScene("1.0.1"); // Replace 1.0.1 with whatever scene you want to load when uberduck dies
                }
                else
                {
                    if (uwr.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogError("Protocol error occurred while downloading audio clip.");
                    }
                    else
                    {
                        audioSource.clip = DownloadHandlerAudioClip.GetContent(uwr);

                        // Shuts up gary
                        if (d.character == "Gary")
                        {
                            audioSource = GetComponent<AudioSource>();
                            audioSource.clip = audioClips[1];
                            audioSource.Play();

                            yield return new WaitForSeconds(5);

                            audioSource.Stop();
                        }
                        // loudward
                        else if (d.character == "Squidward")
                        {
                            float[] clipData = new float[audioSource.clip.samples * audioSource.clip.channels];
                            audioSource.clip.GetData(clipData, 0);
                            for (int i = 0; i < clipData.Length; i++)
                            {
                                clipData[i] *= 1.5f;
                            }

                            audioSource.clip.SetData(clipData, 0);
                        }
                        audioSource.Play();

                        // Play the animation on the character object
                        float audioLength = audioSource.clip.length;
                        Animator characterAnimator = character.GetComponent<Animator>();

                        // Check if the characterAnimator exists
                        if (characterAnimator != null)
                        {
                            characterAnimator.SetBool("Speaking", true);

                            // Wait for the duration of the audio clip
                            yield return new WaitForSeconds(audioLength);

                            // Stop the animation by resetting the bool parameter
                            characterAnimator.SetBool("Speaking", false);

                            // Wait for a short duration to allow the animation to stop
                            yield return new WaitForSeconds(0.1f);
                        }

                        while (audioSource.isPlaying)
                        {
                            yield return null;
                        }
                    }
                }
            }
        }
    }
}
