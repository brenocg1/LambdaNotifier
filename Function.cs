using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaNotifier;

public class Function
{
    public async Task<int> FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
    {
        var logger = context.Logger;

        logger.LogInformation($"Começando a processar LamdbaNotifier");

        logger.LogInformation($"Evento que chegou: " + JsonSerializer.Serialize(dynamoEvent));

        var client = new AmazonDynamoDBClient();

        var newQuote = new QuotationModel();

        foreach (var record in dynamoEvent.Records)
        {
            if (record.EventName == "INSERT")
            {
                // Pegando a imagem nova
                var newImage = record.Dynamodb.NewImage;

                newQuote.quote_value = decimal.Parse(newImage["quote_value"].N);
                newQuote.quotation_id = newImage["quotation_id"].S;
                newQuote.timestamp = DateTime.Parse(newImage["timestamp"].S);
            }
        }

        string tableName = "tbl_usdbrl";
        string sortKey = "timestamp";
        string attributeToRetrieve = "quote_value";

        var scanRequest = new Amazon.DynamoDBv2.Model.ScanRequest
        {
            TableName = tableName,
            ProjectionExpression = "#ts, #qv", // Especifica os atributos a serem retornados
            ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#ts", sortKey },
                    { "#qv", attributeToRetrieve }
                }
        };

        var scanResponse = await client.ScanAsync(scanRequest);
        var items = scanResponse.Items;

        // Ordena os itens pelo timestamp em ordem decrescente e pega os 5 últimos
        var lastFiveItems = items
            .OrderByDescending(item => DateTime.Parse(item[sortKey].S))
            .Take(5)
            .ToList();

        List<double> quoteValues = new List<double>();


        logger.LogInformation("Cotação atual inserida: " + JsonSerializer.Serialize(newQuote));

        logger.LogInformation("-----------------");
        logger.LogInformation("Ultimas 5 cotações registradas: ");
        foreach (var item in lastFiveItems)
        {
            if (item.ContainsKey(sortKey) && item.ContainsKey(attributeToRetrieve))
            {
                double quoteValue = double.Parse(item[attributeToRetrieve].N);
                quoteValues.Add(quoteValue);

                logger.LogInformation($"Cotação do dia {DateTime.Parse(item[sortKey].S)} - Valor: {quoteValue}");
            }
        }

        // Calcular a média das últimas 5 cotações
        double averageQuoteValue = quoteValues.Average();
        double currentQuoteValue = (double)newQuote.quote_value;
        double difference = Math.Abs(currentQuoteValue - averageQuoteValue);
        double percentageDifference = (difference / averageQuoteValue) * 100;

        logger.LogInformation($"Média das últimas 5 cotações: {averageQuoteValue}");
        logger.LogInformation($"Cotação atual: {currentQuoteValue}");
        logger.LogInformation($"Diferença percentual: {percentageDifference}%");

        // Aumentar limite para enviar email
        var percentageThreshold = 20;

        if (percentageDifference > percentageThreshold)
        {
            string subject = "Notificação de Subida de Preço";
            string body = $"A cotação atual ({currentQuoteValue}) subiu mais de 20% em relação à média" +
                $" das últimas 5 cotações ({averageQuoteValue}).";

            await SendEmailAsync("breno.1803@hotmail.com", "brenocampos1803@gmail.com", subject, body);

            context.Logger.LogInformation("Finalizando de processar LamdbaNotifier");

            Console.WriteLine("Notificando subida de preço");

            return 1;
        }
        return 0;
    }

    private static async Task SendEmailAsync(string fromAddress, string toAddress, string subject, string body)
    {
        using (var client = new AmazonSimpleEmailServiceClient())
        {
            var sendRequest = new SendEmailRequest
            {
                Source = fromAddress,
                Destination = new Destination
                {
                    ToAddresses = new List<string> { toAddress }
                },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Text = new Content(body)
                    }
                }
            };

            try
            {
                var response = await client.SendEmailAsync(sendRequest);
                Console.WriteLine("Email sent with message ID: " + response.MessageId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send email: " + ex.Message);
            }
        }
    }
}