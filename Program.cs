using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections;
using Deepgram;
using Deepgram.Logger;
using Deepgram.Microphone;
using Deepgram.Models.Authenticate.v1;
using Deepgram.Models.Listen.v1.WebSocket;
using DotnetGeminiSDK.Client;
using EdgeTTS;
using NAudio.Wave;

namespace SampleApp
{
    class VoiceRecognition
    {
        public string DeepGramAPIKEY;
        public Jarvis jarvis;
        /// <summary>
        /// if youre using recognizemode
        /// </summary>
        public string? recognizeWord;
        
        public enum VoiceMode
        {
            SPEECH_TO_TEXT ,
            TEXT_TO_SPEECH
        }


        public VoiceRecognition(string? apikey)
        {
            if (apikey != null) { this.DeepGramAPIKEY = apikey; }
            this.jarvis = new Jarvis("AIzaSyD7jwAqp9564NLeehBb2PWJYRWKWrfs2L4");
        }

        public void Play(VoiceMode mode)
        {
            if (mode == VoiceMode.SPEECH_TO_TEXT)
            {
                STT();
            }
        }

        private void STT()
        {
            Deepgram.Library.Initialize();
            Deepgram.Microphone.Library.Initialize();


            DeepgramWsClientOptions options = new DeepgramWsClientOptions(DeepGramAPIKEY, null, true);

            var liveClient = new ListenWebSocketClient("", options);

            liveClient.Subscribe(new EventHandler<OpenResponse>((sender, e) =>
            {
                Console.WriteLine($"\n\n----> {e.Type} received");
            }));

            liveClient.Subscribe(new EventHandler<ResultResponse>((sender, e) =>
            {
                // Console.WriteLine("Transcription received: " + JsonSerializer.Serialize(e.Transcription));
                if (e.Channel.Alternatives[0].Transcript.Trim() == "")
                {
                    return;
                }
                
                if (e.IsFinal.HasValue && e.IsFinal.Value)
                {

                    //Control the responses 
                    if (TextToSpeech.isSpeak) { return; }
                    else
                    {
                        Console.WriteLine("You speak : " + e.Channel.Alternatives[0].Transcript);
                        recognizeWord = e.Channel.Alternatives[0].Transcript;
                        string lowerCase = recognizeWord.ToLower();


                        if (lowerCase.Contains("what time is it"))
                        {
                            Console.WriteLine("The time is " + DateTime.Now.ToString("HH:mm"));
                        }
                        else
                        {
                            string answer = jarvis.MessagePrompt(recognizeWord);
                            Console.WriteLine("Jarvis Answer : " + answer);
                            TextToSpeech.TTS(answer);
                        }

                    }


                    

                    return;
                }
            }));

            liveClient.Subscribe(new EventHandler<SpeechStartedResponse>((sender , e) => {

                if (!TextToSpeech.isSpeak) { Console.WriteLine("----- Speak -----"); }
            
            }));



            // Mulai Koneksi pake Skema ini
            var liveSchema = new LiveSchema()
            {
                Model = "nova-2",
                Encoding = "linear16",
                SampleRate = 16000,
                Punctuate = true,
                SmartFormat = true,
                InterimResults = true,
                UtteranceEnd = "1000",
                VadEvents = true,
                
            };
            liveClient.Connect(liveSchema);
            
            // Microphone streaming
            var microphone = new Microphone(liveClient.Send);

            microphone.Start();

            Console.ReadKey();

            microphone.Stop();
            liveClient.Stop();

            Deepgram.Microphone.Library.Terminate();
            Deepgram.Library.Terminate();
        }

    }
    class Jarvis
    {
        private GeminiClient jarvisBot;

        public Jarvis(string apikey)
        {
            jarvisBot = new GeminiClient(new DotnetGeminiSDK.Config.GoogleGeminiConfig
            {
                ApiKey = apikey
            });
            
        }

        public string MessagePrompt(string message)
        {
            if (message == string.Empty) { return string.Empty; }

            message = "You are Jarvis , jarvis is a Professional assistant who help everyone with happy. you created by Hadist" + message;

            return jarvisBot.TextPrompt(message).Result.Candidates[0].Content.Parts[0].Text;
        }


    }
    class TextToSpeech
    {
        public static bool isSpeak = false;
        public static void TTS(string message)
        {
            var client = ClientFactory.CreateSpeakRESTClient("e04cd4ea31fe259dc24b1f1b5b17db1edcb90de9");

            var result = client.ToStream(new Deepgram.Models.Speak.v1.REST.TextSource(message), new Deepgram.Models.Speak.v1.REST.SpeakSchema { Model = "aura-helios-en" });
            
            MemoryStream stream = result.Result.Stream;

            //Guid id = Guid.NewGuid();
            if (File.Exists("D:\\DeepGram\\output.mp3"))
            {
                try
                {
                    File.Delete("D:\\DeepGram\\output.mp3"); //hapus File dahulu
                    FileStream newOne = File.Open("D:\\DeepGram\\output.mp3", FileMode.Create);
                    newOne.Write(stream.ToArray());
                    newOne.Close();

                    Mp3FileReader reader = new Mp3FileReader("D:\\DeepGram\\output.mp3");
                    WaveOutEvent speaker = new WaveOutEvent();
                    speaker.Init(reader);
                    speaker.PlaybackStopped += (object sender, StoppedEventArgs e) =>
                    {
                        isSpeak = false;
                        Console.WriteLine("Stopped");
                        reader.Close();
                    };

                    speaker.Play();

                    if (speaker.PlaybackState == PlaybackState.Playing)
                    {
                        isSpeak = true;
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                

            }
            else
            {
                FileStream file = File.Open("D:\\DeepGram\\output.mp3", FileMode.Create);
                file.Write(stream.ToArray());

                file.Close();

                Mp3FileReader reader = new Mp3FileReader("D:\\DeepGram\\output.mp3");
                WaveOutEvent speaker = new WaveOutEvent();
                speaker.Init(reader);

                speaker.PlaybackStopped += (object sender, StoppedEventArgs e) =>
                {
                    isSpeak = false;
                    Console.WriteLine("Stopped");
                    reader.Close();
                };

                speaker.Play();

                if (speaker.PlaybackState == PlaybackState.Playing)
                {
                    isSpeak = true;
                }


            }
            
        }

        static void WriteWavHeader(BinaryWriter writer, int dataLength, int sampleRate, int bitsPerSample, int channels)
        {
            int blockAlign = (bitsPerSample / 8) * channels;
            int averageBytesPerSecond = sampleRate * blockAlign;

            writer.Write(new[] { 'R', 'I', 'F', 'F' }); // RIFF header
            writer.Write(36 + dataLength); // File size minus the first 8 bytes of RIFF header
            writer.Write(new[] { 'W', 'A', 'V', 'E' }); // WAVE header
            writer.Write(new[] { 'f', 'm', 't', ' ' }); // fmt chunk
            writer.Write(16); // Length of format chunk (16 for PCM)
            writer.Write((short)1); // Audio format (1 = PCM)
            writer.Write((short)channels); // Number of channels
            writer.Write(sampleRate); // Sample rate
            writer.Write(averageBytesPerSecond); // Byte rate
            writer.Write((short)blockAlign); // Block align
            writer.Write((short)bitsPerSample); // Bits per sample
            writer.Write(new[] { 'd', 'a', 't', 'a' }); // Data chunk
            writer.Write(dataLength); // Data size
        }

    }

    class Program
    {

        static async Task Main(string[] args)
        {
            VoiceRecognition engine = new VoiceRecognition("e04cd4ea31fe259dc24b1f1b5b17db1edcb90de9");

            engine.Play(VoiceRecognition.VoiceMode.SPEECH_TO_TEXT);
            //Console.WriteLine("Final Result : " + engine.recognizeWord);
            

            

            Console.ReadKey();
            
        }

    }
}