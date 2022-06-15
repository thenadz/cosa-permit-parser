using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NeighborhoodPermitParser;
using NeighborhoodPermitParser.Serializers;

namespace NeighborhoodPermitParserCLI
{
    class Program
    {
        private static readonly string CREDENTIALS = Path.Combine(Utilities.AssemblyDirectory, ".credentials");
        private static readonly string EMAIL_TEMPLATE = Path.Combine(Utilities.AssemblyDirectory, "email-template.html");

        static void Main(string[] args)
        {
            PermitRunner runner = new PermitRunner();

            string htmlTemplate = File.ReadAllText(EMAIL_TEMPLATE);
            CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                NewLine = "\r\n",
                LeaveOpen = true
            };

            // Parse credentials file - not elegant, but good enough for what we need
            Dictionary<string, string> creds = File.ReadAllLines(CREDENTIALS).Select(l => l.Split('=', StringSplitOptions.TrimEntries)).ToDictionary(l => l[0], l => l[1]);

            using SmtpClient smtp = new SmtpClient();
            smtp.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            smtp.Authenticate(creds["username"], creds["password"]);

            foreach ((NeighborhoodListing neighborhood, HashSet<PermitEntry> permits) in runner.NeighborhoodsWithPermits)
            {
                MimeMessage email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(creds["username"]));
                email.To.Add(MailboxAddress.Parse(neighborhood.Email));
                email.Subject = $"{neighborhood.Name} Permit Report - {DateTime.Now:d}";

                BodyBuilder builder = new BodyBuilder();

                // Build personalized HTML message for neighborhood
                builder.HtmlBody = htmlTemplate
                    .Replace("###PREHEADER", $"{neighborhood.Name} Permit Report")
                    .Replace("###NAME", neighborhood.PocFirstName)
                    .Replace("###NEIGHBORHOOD", neighborhood.Name)
                    .Replace("###COUNT", permits.Count.ToString());

                // Build CSV of neighborhood permits in memory to be sent as attachment
                byte[] csvBytes;
                using (MemoryStream ms = new MemoryStream())
                using (TextWriter tw = new StreamWriter(ms))
                using (CsvWriter writer = new CsvWriter(tw, csvConfig))
                {
                    writer.WriteHeader<PermitEntry>();
                    writer.NextRecord();
                    writer.WriteRecords(permits.OrderBy(p => p.DateSubmitted));
                    writer.Flush();
                    csvBytes = ms.ToArray();
                }

                builder.Attachments.Add($"{neighborhood.Name} Permit Report.csv", csvBytes);
                email.Body = builder.ToMessageBody();
                smtp.Send(email);
            }

            smtp.Disconnect(true);
        }
    }
}
