using Discord;
using Discord.WebSocket;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;
using System.Reactive.Linq;

namespace Agatha2
{
	internal class ModuleMarkov : BotModule
	{
   		private bool hasDictionaryChanged = false;
		private Dictionary<string, List<string>> markovDict;
		internal int markovChance = 0;

		internal ModuleMarkov()
		{
			moduleName = "Markov";
			description = "Listens to chatter and produces Markov string responses.";
		}
		internal override void StartModule()
		{
			IObservable<long> periodicSaveTimer = Observable.Interval(TimeSpan.FromMinutes(10));
			CancellationTokenSource source = new CancellationTokenSource();
			Action action = (() => 
			{
				TrySaveDictionary();
			}
			);
			periodicSaveTimer.Subscribe(x => { Task task = new Task(action); task.Start();}, source.Token);
		}
		internal override bool Register(List<BotCommand> commands)
		{
			Console.WriteLine("Deserializing Markov dictionary.");
			try
			{
				using (var file = File.OpenRead("data/markov.bin")) {
					markovDict = Serializer.Deserialize<Dictionary<string, List<string>>>(file);
				}
			}
			catch (FileNotFoundException ex)
			{
				Console.WriteLine(ex);
				markovDict = new Dictionary<string, List<string>>();
			}
			commands.Add(new CommandReplyrate());
			return true;
		}

		private void TrySaveDictionary()
		{
			if(hasDictionaryChanged)
			{
				hasDictionaryChanged = false;
				Console.WriteLine("Serializing Markov dictionary.");
				using (var file = File.Create("data/markov.bin")) {
					Serializer.Serialize(file, markovDict);
				}
			}
		}

		internal string GetMarkovChain(string initialToken)
		{
			string result = "";

			if(markovDict.ContainsKey(initialToken))
			{
				int max_tokens = 30;
				string token = initialToken;
				result = $"{token}";
				while(max_tokens > 0 && markovDict.ContainsKey(token) && markovDict[token] != null && markovDict[token].Count > 0)
				{
					token = markovDict[token][Program.rand.Next(markovDict[token].Count)];
					result += $" {token}";
					max_tokens--;
				}
			}
			result = result[0].ToString().ToUpper() + result.Substring(1);
			string finalChar = result.Substring(result.Length-1, 1);
			if(finalChar != "." && finalChar != "!" && finalChar != "?")
			{
				result += ".";
			}
			return result;
		}

		internal override void ListenTo(SocketMessage message)
		{
			string lastString = null;
			foreach(string token in message.Content.Split(" "))
			{
				if(markovDict == null)
				{
					return;
				}
				if(!markovDict.ContainsKey(token))
				{
					markovDict.Add(token, new List<string>());
					hasDictionaryChanged = true;
				}
				if(lastString != null && lastString != "")
				{
					if(markovDict.ContainsKey(lastString) && markovDict[lastString] == null)
					{
						markovDict.Remove(lastString);
					}
					if(!markovDict.ContainsKey(lastString))
					{
						markovDict.Add(lastString, new List<string>());
					}
					if(!markovDict[lastString].Contains(token))
					{
						markovDict[lastString].Add(token);
						hasDictionaryChanged = true;
					}
				}
				lastString = token;
			}
			string searchSpace =  message.Content.ToLower();
			if((markovChance > 0 && Program.rand.Next(100) <= markovChance) || searchSpace.Contains("agatha"))
			{
				string[] tokens = message.Content.Split(" ");
				if(tokens.Length > 0)
				{
					string markovText = GetMarkovChain(tokens[Program.rand.Next(tokens.Length)]);
					if(markovText != null && markovText != "") 
					{
						Task.Run( () => message.Channel.SendMessageAsync(markovText));
					}
				}
			}
		}
	}
}