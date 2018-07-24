using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;

namespace RecognizeSpeech
{
    class Program
    {
        public static async Task RecognizeSpeechAsync()
        {
            // Creates an instance of a speech factory with specified
            // subscription key and service region. Replace with your own subscription key
            // and service region (e.g., "westus").
            var factory = SpeechFactory.FromSubscription("f93eb671db224bc68f499e8fe734a084", "northeurope");

            // Creates a speech recognizer.
            using (var recognizer = factory.CreateSpeechRecognizer())
            {
                Console.WriteLine("Say something...");

                // Performs recognition.
                // RecognizeAsync() returns when the first utterance has been recognized, so it is suitable 
                // only for single shot recognition like command or query. For long-running recognition, use
                // StartContinuousRecognitionAsync() instead.
                var result = await recognizer.RecognizeAsync();

                // Checks result.
                if (result.RecognitionStatus != RecognitionStatus.Recognized)
                {
                    Console.WriteLine($"Recognition status: {result.RecognitionStatus.ToString()}");
                    if (result.RecognitionStatus == RecognitionStatus.Canceled)
                    {
                        Console.WriteLine($"There was an error, reason: {result.RecognitionFailureReason}");
                    }
                    else
                    {
                        Console.WriteLine("No speech could be recognized.\n");
                    }
                }
                else
                {
                    Console.WriteLine($"We recognized: {result.Text}");
                  //  string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                   // using (StreamWriter outputFile = new StreamWriter(Path.Combine(mydocpath, "WriteLines.txt"), true))
                   // {
                     //   outputFile.WriteLine("Fourth Line";
                    //}
                }
            }

        }
        static void Main(string[] args)
        {
            RecognizeSpeechAsync().Wait();
            Console.WriteLine("Please press a key to continue.");
            Console.ReadLine();
        }
    }
}
