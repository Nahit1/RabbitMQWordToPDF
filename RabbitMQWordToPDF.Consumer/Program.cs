using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQWordToPDF.Shared;
using Spire.Doc;
using System;
using System.IO;
using System.Net.Mail;
using System.Text;

namespace RabbitMQWordToPDF.Consumer
{
    class Program
    {

        public static bool EmailSend(string email, MemoryStream memoryStream, string fileName)
        {
            try
            {
                memoryStream.Position = 0;

                System.Net.Mime.ContentType ct = new System.Net.Mime.ContentType(System.Net.Mime.MediaTypeNames.Application.Pdf);
                Attachment attach = new Attachment(memoryStream, ct);
                MailMessage mailMessage = new MailMessage();
                SmtpClient smtpClient = new SmtpClient();

                attach.ContentDisposition.FileName = $"{fileName}.pdf";

                mailMessage.From = new MailAddress("nahitektas@gmail.com");
                mailMessage.To.Add(email);

                mailMessage.Subject = "Pdf  Dosyası oluşturma | nahitektas.com";

                mailMessage.Body = "pdf dosyanız ektedir.";

                mailMessage.IsBodyHtml = true;

                mailMessage.Attachments.Add(attach);

                smtpClient.Host = "smtp.gmail.com";
                smtpClient.Port = 587/465;

                smtpClient.Credentials = new System.Net.NetworkCredential("nahitektas@gmail.com", "****");
                smtpClient.Send(mailMessage);
                Console.WriteLine($"Sonuç: {email} adresine gönderilmiştir");

                memoryStream.Close();
                memoryStream.Dispose();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private static void Main(string[] args)
        {
            bool result = false;
            var factory = new ConnectionFactory();

            factory.Uri = new Uri("amqps://vqyjzvkx:vJBnZyoX2xJAW8OEK2CpKEkaRMMApzTF@hornet.rmq.cloudamqp.com/vqyjzvkx");

            using (var connection = factory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.ExchangeDeclare("convertPdfExchange", ExchangeType.Direct, true, false, null);

                    channel.QueueBind("file", "convertPdfExchange", "WordToPdf");

                    channel.BasicQos(0, 1, false);

                    var consumer = new EventingBasicConsumer(channel);

                    channel.BasicConsume("file", false, consumer);

                    consumer.Received += (model, ea) =>
                    {
                        try
                        {
                            Console.WriteLine("Kuyruktan bir mesaj alındı ve işleniyor");

                            Document document = new Document();

                            var body = ea.Body.Span;

                            string message = Encoding.UTF8.GetString(body);

                            MessageWordToPdf messageWordToPdf = JsonConvert.DeserializeObject<MessageWordToPdf>(message);

                            document.LoadFromStream(new MemoryStream(messageWordToPdf.WordByte), FileFormat.Docx2013);

                            using (MemoryStream ms = new MemoryStream())
                            {
                                document.SaveToStream(ms, FileFormat.PDF);

                                result = EmailSend(messageWordToPdf.Email, ms, messageWordToPdf.FileName);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Hata meydana geldi:" + ex.Message);
                        }
                        if (result)
                        {
                            Console.WriteLine("Kuyruktan Mesaj başarıla işlendi...");
                            channel.BasicAck(ea.DeliveryTag, false);
                        }
                    };

                    Console.WriteLine("çıkmak için tıklayınız");
                    Console.ReadLine();
                }
            }
        }
    }
}
