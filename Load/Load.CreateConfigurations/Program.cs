﻿using System.Net;
using System.Text.Json;
using System.Text;
using Bogus;
using ShellProgressBar;

var client = new HttpClient()
{
    BaseAddress = new Uri("https://localhost:18081/") // this should be the address to the EmailService API
};

var faker = new Faker<Person>();
faker.RuleFor(x => x.Name, (f, p) => f.Person.FullName);
faker.RuleFor(x => x.EmailAddress, (f, p) => f.Person.Email);

var total = 1000;

var pb = new ProgressBar(maxTicks: total, "starting...");

var sem = new SemaphoreSlim(1, 1);
var c = 0;

string template =
"""
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
  <head>
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta name="viewport" content="width=device-width" />
      
      
  </head>
  <body style="width: 100%; min-width: 100%; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; margin: 0; padding: 0; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; text-align: left; line-height: 19px; font-size: 14px;">
    <table class="body" style="border-spacing: 0; border-collapse: collapse; padding: 0; vertical-align: top; text-align: left; height: 100%; width: 100%; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">
      <tr style="padding: 0; vertical-align: top; text-align: left;">
        <td class="center" align="center" valign="top" style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 0; vertical-align: top; text-align: center; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">
          <center style="width: 100%; min-width: 580px;">
            <!-- HEADER -->
            <table class="row header" style="border-spacing: 0; border-collapse: collapse; padding: 0px; vertical-align: top; text-align: left; width: 100%; position: relative;">
              <tr style="padding: 0; vertical-align: top; text-align: left;">
                <td class="center" align="center" style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 0; vertical-align: top; text-align: center; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">
                  <center style="width: 100%; min-width: 580px;">

                    <table class="container" style="border-spacing: 0; border-collapse: collapse; padding: 0; vertical-align: top; text-align: inherit; width: 580px; margin: 0 auto;">
                      <tr style="padding: 0; vertical-align: top; text-align: left;">
                        <td class="wrapper last" style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 10px 20px 0px 0px; vertical-align: top; text-align: left; position: relative; padding-right: 0px; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">

                          <table class="twelve columns" style="border-spacing: 0; border-collapse: collapse; padding: 0; vertical-align: top; text-align: left; margin: 0 auto; width: 580px;">
                            <tr class="b-header" style="padding: 0; vertical-align: top; text-align: left;">
                              <td class="six sub-columns" style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 0px 0px 10px; vertical-align: top; text-align: left; padding-right: 10px; min-width: 0px; width: 50%; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">
                                <!-- {{ 'logo.png' | asset_url | img_tag }} -->
                                <img src="http://placehold.it/350x150" alt="" style="outline: none; text-decoration: none; -ms-interpolation-mode: bicubic; width: auto; max-width: 100%; float: left; clear: both; display: block;" />
                              </td>
                              <td class="six sub-columns last" style="text-align: right; vertical-align: middle; word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 0px 0px 10px; padding-right: 0px; min-width: 0px; width: 50%; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">
                              </td>
                              <td class="expander" style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 0; vertical-align: top; text-align: left; visibility: hidden; width: 0px; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;"></td>
                            </tr>
                          </table>

                        </td>
                      </tr>
                    </table>

                  </center>
                </td>
              </tr>
            </table>
            <!-- END OF HEADER -->

            <table class="container" style="border-spacing: 0; border-collapse: collapse; padding: 0; vertical-align: top; text-align: inherit; width: 580px; margin: 0 auto;">
              <tr style="padding: 0; vertical-align: top; text-align: left;">
                <td style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 0; vertical-align: top; text-align: left; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">

                  <table class="row" style="border-spacing: 0; border-collapse: collapse; padding: 0px; vertical-align: top; text-align: left; width: 100%; position: relative; display: block;">
                    <tr style="padding: 0; vertical-align: top; text-align: left;">
                      <td class="wrapper last" style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 10px 20px 0px 0px; vertical-align: top; text-align: left; position: relative; padding-right: 0px; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">

                        <table class="twelve columns" style="border-spacing: 0; border-collapse: collapse; padding: 0; vertical-align: top; text-align: left; margin: 0 auto; width: 580px;">
                          <tr style="padding: 0; vertical-align: top; text-align: left;">
                            <td style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 0px 0px 10px; vertical-align: top; text-align: left; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;">
                              <!-- Email Content -->
                              {% if customer.name %}
<p class="lead" style="margin: 0; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; padding: 0; text-align: left; line-height: 21px; font-size: 18px; margin-bottom: 10px;">Dear {{ customer.name }},</p>
{% endif %}
<p class="lead" style="margin: 0; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; padding: 0; text-align: left; line-height: 21px; font-size: 18px; margin-bottom: 10px;">This is to confirm that your account for the shop {{ shop.name }} is now&nbsp;active.</p>
<p style="margin: 0; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; padding: 0; text-align: left; line-height: 19px; font-size: 14px; margin-bottom: 10px;">The next time you shop at {{ shop.name }}, you can save time at checkout by logging into your account. This will prefill your address information at the checkout. </p>
<p style="margin: 0; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; padding: 0; text-align: left; line-height: 19px; font-size: 14px; margin-bottom: 10px;">Thank you,</p>
<p style="margin: 0; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; padding: 0; text-align: left; line-height: 19px; font-size: 14px; margin-bottom: 10px;">{{ shop.name }}</p>


                            </td>
                            <td class="expander" style="word-break: break-word; -webkit-hyphens: auto; -moz-hyphens: auto; hyphens: auto; border-collapse: collapse; padding: 0; vertical-align: top; text-align: left; visibility: hidden; width: 0px; color: #222222; font-family: Helvetica, Arial, sans-serif; font-weight: normal; margin: 0; line-height: 19px; font-size: 14px;"></td>
                          </tr>
                        </table>

                      </td>
                    </tr>
                  </table>
                <!-- container end below -->
                </td>
              </tr>
            </table>
          </center>
        </td>
      </tr>
    </table>
  </body>
</html>
""";

await Parallel.ForAsync(1, total + 1, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (i, ct) =>
{
    var person = faker.Generate();

    var req = new
    {
        serverUrl = $"mailpit",
        serverPort = "1025",
        fromName = person.Name,
        from = person.EmailAddress,
        userName = "admin",
        password = "admin"
    };

    await client.PostAsync($"config/{i}/smtp", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"), ct);
    await client.PostAsync($"config/{i}/templates", new StringContent(
    $$"""
	{
		"Language": "en",
		"Type": "customer-account-welcome",
		"SubjectTemplate": "customer-account-welcome",
		"BodyTemplate": "{{JsonEncodedText.Encode(template)}}"
	}
	""", Encoding.UTF8, "application/json"), ct);

    var n = Interlocked.Increment(ref c);
    await sem.WaitAsync(ct);
    pb.Tick();
    pb.Message = $"updating... {n}/{total}";

    sem.Release();
});

pb.Message = "Done";

public class Person
{
    public string Name { get; set; }
    public string EmailAddress { get; set; }
}