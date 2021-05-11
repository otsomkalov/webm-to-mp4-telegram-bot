﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Bot.Models;
using Bot.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using File = System.IO.File;
using Message = Telegram.Bot.Types.Message;

namespace Bot.Services
{
    public class DownloaderService : BackgroundService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly ITelegramBotClient _bot;
        private readonly ILogger<DownloaderService> _logger;
        private readonly ServicesSettings _servicesSettings;

        public DownloaderService(ITelegramBotClient bot, ILogger<DownloaderService> logger,
            IOptions<ServicesSettings> servicesSettings, IAmazonSQS sqsClient)
        {
            _bot = bot;
            _logger = logger;
            _sqsClient = sqsClient;
            _servicesSettings = servicesSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunAsync(stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error during Downloader execution:");
                }
                
                await Task.Delay(_servicesSettings.ProcessingDelay, stoppingToken);
            }
        }

        private async Task RunAsync(CancellationToken stoppingToken)
        {
            var response = await _sqsClient.ReceiveMessageAsync(_servicesSettings.DownloaderQueueUrl, stoppingToken);

            var queueMessage = response.Messages.FirstOrDefault();
            
            if (queueMessage is null) return;
            
            var (receivedMessage, sentMessage, linkOrFileName) = JsonSerializer.Deserialize<DownloaderMessage>(queueMessage.Body)!;

            await _bot.EditMessageTextAsync(
                new(sentMessage.Chat.Id),
                sentMessage.MessageId,
                $"{linkOrFileName}\nDownloading file 🚀",
                cancellationToken: stoppingToken);
            
            var inputFilePath = $"{Path.GetTempPath()}{Guid.NewGuid()}.webm";
            
            if (string.IsNullOrEmpty(linkOrFileName))
            {
                await HandleDocumentAsync(receivedMessage, sentMessage, inputFilePath);
            }
            else
            {
                await HandleLinkAsync(receivedMessage, sentMessage, linkOrFileName, inputFilePath);
            }

            await _bot.EditMessageTextAsync(
                new(sentMessage.Chat.Id),
                sentMessage.MessageId,
                $"{linkOrFileName}\nYour file is waiting to be converted 🕒",
                cancellationToken: stoppingToken);
            
            await _sqsClient.DeleteMessageAsync(_servicesSettings.DownloaderQueueUrl, queueMessage.ReceiptHandle, stoppingToken);
        }

        private async Task HandleLinkAsync(Message receivedMessage, Message sentMessage, string linkOrFileName, string inputFilePath)
        {
            using var webClient = new WebClient();

            try
            {
                await webClient.DownloadFileTaskAsync(linkOrFileName, inputFilePath);

                await SendMessageAsync(receivedMessage, sentMessage, inputFilePath,
                    linkOrFileName);
            }
            catch (WebException webException)
            {
                if (webException.Response is HttpWebResponse response)
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:

                            await _bot.EditMessageTextAsync(
                                new(sentMessage.Chat.Id),
                                sentMessage.MessageId,
                                $"{linkOrFileName}\nI am not authorized to download video from this source 🚫");

                            return;

                        case HttpStatusCode.NotFound:

                            await _bot.EditMessageTextAsync(
                                new(sentMessage.Chat.Id),
                                sentMessage.MessageId,
                                $"{linkOrFileName}\nVideo not found ⚠️");

                            return;

                        case HttpStatusCode.InternalServerError:

                            await _bot.EditMessageTextAsync(
                                new(sentMessage.Chat.Id),
                                sentMessage.MessageId,
                                $"{linkOrFileName}\nServer error 🛑");

                            return;
                    }
                }
            }
        }

        private async Task HandleDocumentAsync(Message receivedMessage, Message sentMessage, string inputFileName)
        {
            await using (var fileStream = File.Create(inputFileName))
            {
                await _bot.GetInfoAndDownloadFileAsync(receivedMessage.Document.FileId, fileStream);
            }

            await SendMessageAsync(receivedMessage, sentMessage, inputFileName, receivedMessage.Document.FileName);
        }

        private async Task SendMessageAsync(Message receivedMessage, Message sentMessage, string inputFilePath,
            string linkOrFilename)
        {
            var converterMessage = new ConverterMessage(receivedMessage, sentMessage, inputFilePath, linkOrFilename);

            await _sqsClient.SendMessageAsync(_servicesSettings.ConverterQueueUrl, JsonSerializer.Serialize(converterMessage));
        }
    }
}