using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Microsoft.Extensions.Configuration;

namespace IntentFromSpeech
{
    class Program
    {
        // configuration used for dotnet user-secrets
        private static IConfigurationRoot Configuration;

        public static async Task RecognizeSpeechAsync()
        {
            // Creates an instance of a speech config with specified subscription key and service region.
            // Replace with your own subscription key // and service region (e.g., "westus").
            //var config = SpeechConfig.FromSubscription("YourSubscriptionKey", "YourServiceRegion");
            // get the correct URI from 
            // https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/sovereign-clouds#speech-sdk
            Uri uriUsGov = new Uri(Configuration["SecretStrings:uriUsGov"]);
            // get this from the https://portal.azure.us/
            var config = SpeechConfig.FromHost(uriUsGov, Configuration["SecretStrings:speechKey"]);  

            // Creates a speech recognizer.
            using (var recognizer = new SpeechRecognizer(config))
            {
                Console.WriteLine("Say something...");

                // Starts speech recognition, and returns after a single utterance is recognized. The end of a
                // single utterance is determined by listening for silence at the end or until a maximum of 15
                // seconds of audio is processed.  The task returns the recognition text as result. 
                // Note: Since RecognizeOnceAsync() returns only a single utterance, it is suitable only for single
                // shot recognition like command or query. 
                // For long-running multi-utterance recognition, use StartContinuousRecognitionAsync() instead.
                var result = await recognizer.RecognizeOnceAsync();

                // Checks result.
                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"We recognized: {result.Text}");
                    await RecognizeIntentAsync(result.Text);
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        Console.WriteLine($"CANCELED: Did you update the subscription info?");
                    }
                }
            }
        }

        public static async Task RecognizeIntentAsync(string szRecognizedText)
        {
            //get this from https://luis.azure.us/ 
            var luisPredictionKey = Configuration["SecretStrings:luisPredictionKey"];
            //get this from https://luis.azure.us/
            var luisPredictionEndpoint = Configuration["SecretStrings:luisPredictionEndpoint"];
            //get this from https://luis.azure.us/ 
            System.Guid luisAppId = new System.Guid(Configuration["SecretStrings:luisAppId"]);
            
            var credentials = new Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.ApiKeyServiceClientCredentials(luisPredictionKey);
            var runtimeClient = new LUISRuntimeClient(credentials) { Endpoint = luisPredictionEndpoint };

            var request = new PredictionRequest { Query = szRecognizedText };
            var prediction = await runtimeClient.Prediction.GetSlotPredictionAsync(luisAppId, "Production", request);
            
            // Show the raw results
            Console.WriteLine("Raw JSON Returned:");

            // Making the JSON look pretty before outputing to console
            JsonSerializerOptions opt = new JsonSerializerOptions();
            opt.WriteIndented = true;
            Console.Write(JsonSerializer.Serialize(prediction, opt));

            // The top intent identified
            Console.WriteLine("");
            Console.WriteLine($"Top Intent Identified: {prediction.Prediction.TopIntent}");
        }
        
        // ===============================================================================================
        // This Boot-strap section is needed for Local Development Settings
        // Used to protect secrets on local machines not checked into code, like connection strings.
        // https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-5.0
        // ===============================================================================================
        private static void BootstrapConfiguration()
        {
            string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isDevelopment = string.IsNullOrEmpty(env) ||  env.ToLower() == "development";

            var builder = new ConfigurationBuilder();

            //set the appsettings file for general configuration (stuff that's not secret)
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            //if development, load user secrets as well
            if (isDevelopment)
            {
                builder.AddUserSecrets<Program>();
            }

            Configuration = builder.Build();
            return;
            // ===============================================================
        }

        static void Main(string[] args)
        {
            // Just used to get secret values from json file
            BootstrapConfiguration();

            RecognizeSpeechAsync().Wait();
            Console.WriteLine("Please press <Return> to continue.");
            Console.ReadLine();
        }
    }
}
