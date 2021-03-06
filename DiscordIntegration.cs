﻿using System;
using System.IO;
using Discord;
using Discord.WebSocket;
using GTANetworkAPI;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Discord.Rest;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;

namespace Integration
{
	public class DiscordIntegration : Script
	{
		static DiscordSocketClient discord = null;
		private static string m_strToken = null;
		private static string m_strBotGameName = null;
		private static ActivityType m_eActivityType = ActivityType.Playing;
		private static UserStatus m_eUsterStatus = UserStatus.Online;

		/// <summary>
		/// This is the first method that should be called to start the bot instance. With the token provided we can initialize the bot and start logging in to fire it.
		/// </summary>
		/// <param name="strToken"></param>
		/// <param name="strBotGameName"></param>
		/// <param name="eActivityType"></param>
		/// <param name="eUserStatus"></param>
		public static void SetUpBotInstance(string strToken, string strBotGameName = null, ActivityType eActivityType = ActivityType.CustomStatus, UserStatus eUserStatus = UserStatus.Online)
		{
			if (!IsSetupCompleted)
			{
				m_strToken = strToken;
				m_strBotGameName = strBotGameName;
				m_eActivityType = eActivityType;
				m_eUsterStatus = eUserStatus;

				InitAsync();
				IsSetupCompleted = true;
			}
			else
			{
				ThrowErrorMessage("You can only have one bot instance running. You can only call this method once.");
			}
		}

		private static async void InitAsync()
		{
			if (!string.IsNullOrEmpty(m_strToken))
			{
				discord = new DiscordSocketClient();

				discord.Connected += OnReady;
				discord.MessageReceived += OnMessageReceived;

				await discord.LoginAsync(TokenType.Bot, m_strToken).ConfigureAwait(true);
				await discord.StartAsync().ConfigureAwait(true);
			}
			else
			{
				ThrowErrorMessage("Object reference not set to an instance of an object\nUse the 'SetUpBotInstance' to provide your token to be able to start the bot");
			}
		}

		private static Task OnMessageReceived(SocketMessage message)
		{
			if (IsSetupCompleted)
			{
				NAPI.Task.Run(() =>
				{
					try
					{
						if (g_lstSubscribedChannels.Contains(message.Channel.Id))
						{
							if (message.Content.Length > 0)
							{
								if (!message.Author.IsBot)
								{
									string strFormattedMessage = $"[DISCORD] {message.Author.Username}: {message.Content}";
									NAPI.Chat.SendChatMessageToAll(strFormattedMessage);
								}
							}
						}
					}
					catch
					{

					}
				});

				Task task = Task.Run(() => { });
				return task;
			}
			return null;
		}

		/// <summary>
		/// Sends a message on discord in the specified channel.
		/// Throws an error if the channel can't be found.
		/// </summary>
		/// <param name="discordChannelID"></param>
		/// <param name="strMessage"></param>
		/// <returns></returns>
		public static async Task SendMessage(ulong discordChannelID, string strMessage, bool bStripMentions = true)
		{
			if (IsSetupCompleted)
			{
				try
				{
					ISocketMessageChannel channel = (ISocketMessageChannel)discord.GetChannel(discordChannelID);

					if (channel != null)
					{
						RestUserMessage msg = await channel.SendMessageAsync(bStripMentions ? CleanFromTags(strMessage) : strMessage).ConfigureAwait(true);
					}
					else
					{
						ThrowErrorMessage("Object reference not set to an instance of an object\nFailed to find a discord channel with the 'discordChannelID' you provided");
						return;
					}
				}
				catch
				{

				}
			}
		}

		private static async Task OnReady()
		{
			await UpdateStatus(m_strBotGameName, m_eActivityType, m_eUsterStatus).ConfigureAwait(true);
		}

		/// <summary>
		/// Update the status of the bot. UserStatus works (sometimes).
		/// </summary>
		/// <param name="strBotGameName"></param>
		/// <param name="eActivityType"></param>
		/// <param name="eUserStatus"></param>
		/// <returns></returns>
		public static async Task UpdateStatus(string strBotGameName, ActivityType eActivityType, UserStatus eUserStatus)
		{
			m_strBotGameName = strBotGameName;
			m_eActivityType = eActivityType;
			m_eUsterStatus = eUserStatus;

			Game game = new Game(m_strBotGameName, m_eActivityType);
			await discord.SetStatusAsync(m_eUsterStatus).ConfigureAwait(true);
			await discord.SetActivityAsync(game).ConfigureAwait(true);
		}

		/// <summary>
		/// Register a channel for the bot to listen and send messages to all the players in the RAGEMP server
		/// </summary>
		/// <param name="channelID"></param>
		/// <returns>bool if succeeded or not</returns>
		public static bool RegisterChannelForListenting(ulong channelID)
		{
			if(g_lstSubscribedChannels.Contains(channelID))
			{
				ThrowErrorMessage("Couldn't add the channel to the list as it already exists there. Remove it or check the channel ID");
				return false;
			}

			ISocketMessageChannel channel = (ISocketMessageChannel)discord.GetChannel(channelID);
			if(channel != null)
			{
				g_lstSubscribedChannels.Add(channelID);
				return true;
			}
			else
			{
				ThrowErrorMessage("Object reference not set to an instance of an object\nDiscord channel not found. Can't add a null reference to the list of subscribed channels.");
			}

			return false;
		}

		/// <summary>
		/// Removes a channel for the bot to STOP listening and STOP send messages to all the players in the RAGEMP server
		/// </summary>
		/// <param name="channelID"></param>
		/// <returns>bool if succeeded or not</returns>
		public static bool RemoveChannelFromListening(ulong channelID)
		{
			if(g_lstSubscribedChannels.Contains(channelID))
			{
				g_lstSubscribedChannels.Remove(channelID);
				return true;
			}
			else
			{
				ThrowErrorMessage("Couldn't remove the channel from the list because it doesn't exist in it. Add it or check the channel ID");
			}

			return false;
		}


		/// <summary>
		/// Clears the message from mentions by replacing "@" with "@ "
		/// </summary>
		/// <param name="strInput"></param>
		/// <returns>string without mention</returns>
		private static string CleanFromTags(string strInput)
		{
			return strInput.Replace("@", "@‎‏‏‎ ‎");
		}

		private static void ThrowErrorMessage(string StrErrorMessage)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(StrErrorMessage);
			Console.ResetColor();
		}


		public static bool IsSetupCompleted { get; private set; } = false;
		private static List<ulong> g_lstSubscribedChannels = new List<ulong>();
	}
}
