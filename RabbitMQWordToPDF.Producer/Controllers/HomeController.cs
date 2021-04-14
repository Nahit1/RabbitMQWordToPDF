using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQWordToPDF.Producer.Models;
using RabbitMQWordToPDF.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQWordToPDF.Producer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }


        [HttpPost]
        public IActionResult WordToPdf(WordToPdf wordToPdf)
        {
            var factory = new ConnectionFactory();

            factory.Uri = new Uri(_configuration["ConnectionStrings:RabbitMQCloudString"]);

            //Bağlantı oluşturuldu
            using (var connection = factory.CreateConnection())
            {
                //Kanal açıldı
                using (var channel = connection.CreateModel())
                {
                    //Exchange Oluşturma
                    channel.ExchangeDeclare("convertPdfExchange", ExchangeType.Direct, true, false, null);

                    //Kuyruk Oluşturma
                    channel.QueueDeclare("file", true, false, false, null);

                    //Kuyruk exchange e bind ediliyor.
                    channel.QueueBind("file", "convertPdfExchange", "WordToPdf");

                    //Shared library de oluşturulan sınıfa gelen veri taşınır.
                    MessageWordToPdf messageWordToPdf = new MessageWordToPdf();

                    using (MemoryStream ms = new MemoryStream())
                    {
                        wordToPdf.WordFile.CopyTo(ms);
                        messageWordToPdf.WordByte = ms.ToArray();
                    }

                    messageWordToPdf.Email = wordToPdf.Email;
                    messageWordToPdf.FileName = Path.GetFileNameWithoutExtension(wordToPdf.WordFile.FileName);

                    //Serialize İşlemi
                    string serializeMessage = JsonConvert.SerializeObject(messageWordToPdf);
                    //Serialize edilen veri byte a encode edilir.
                    byte[] byteMessage = Encoding.UTF8.GetBytes(serializeMessage);

                    //Mesajların instance üzerinden kaybolmaması için
                    var properties = channel.CreateBasicProperties();
                    properties.Persistent = true;

                    //Gönderim yapılır
                    channel.BasicPublish("convertPdfExchange", "WordToPdf", properties, byteMessage);

                    
                    return RedirectToAction("Index");

                }
            }

            
        }

    }
}
