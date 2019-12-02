using MihaZupan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Mail;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.ObjectModel;

namespace WhoAdminBot
{

    public class Bot
    {
        private static ITelegramBotClient BotClient;
        static object locker { get; set; } = new object();
        static ObservableCollection<User> Users { get; set; } = new ObservableCollection<User> { };
        public static void Start()
        {
            Users.CollectionChanged += Users_CollectionChanged;
            BotClient = new TelegramBotClient("Токен вашего бота") { Timeout = TimeSpan.FromSeconds(10) };
            var bot = BotClient.GetMeAsync().Result;
            BotClient.OnMessage += Bot_OnMessage;
            BotClient.StartReceiving();
        }
        public static void Stop()
        {
            BotClient.StopReceiving();
        }
        public static void Reboot()
        {
            Stop();
            System.Threading.Thread.Sleep(60000);
            Start();
        }
        private static void Users_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            try
            {
                if (Users.Count > 3000)
                {
                    lock (locker)
                        Users = new ObservableCollection<User>(Users.Skip(2900));
                }
            }
            catch (Exception)
            { Reboot(); }
        }
        private async static void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            try
            {
                var text = e?.Message?.Text;
                if (text == null)
                    return;
                if (text.Contains("/start") || text.Contains("Отмена"))
                {
                    var keyboard = GetKeyboard(new List<string> { "Подобрать" });
                    await BotClient.SendTextMessageAsync(e.Message.Chat, "Здравстуйте, подберите авто", Telegram.Bot.Types.Enums.ParseMode.Default, false, false, 0, keyboard);
                    var user = new User { Id = e.Message.From.Id, Username = e.Message.From.Username, Status = Status.Started };
                    lock (locker)
                    {
                        var baseuser = Users.FirstOrDefault(x => x.Id == user.Id);
                        if (baseuser != null)
                            Users.Remove(baseuser);
                        Users.Add(user);
                    }
                }
                else
                {
                    if (Users.Any(x => x.Id == e.Message.From.Id))
                    {
                        var user = Users.First(x => x.Id == e.Message.From.Id);
                        if (user.Status == Status.Started)
                        {
                            var keyboard = GetKeyboard(new List<string> { "7000 - 10 000$", "10 000 - 15000$", "15 000 - 20 000$", "20 000$+" });
                            await BotClient.SendTextMessageAsync(e.Message.Chat, "Пожалуйста, выберите пункт из меню ниже", Telegram.Bot.Types.Enums.ParseMode.Default, false, false, 0, keyboard);
                            lock (locker)
                                Users.FirstOrDefault(x => x.Id == user.Id).Status = Status.WaitMoney;
                            return;
                        }
                        if (user.Status == Status.WaitMoney)
                        {
                            var RemoveKeyboard = new ReplyKeyboardRemove();
                            var msg = await BotClient.SendTextMessageAsync(e.Message.Chat, "Отлично! Теперь напишите модель автомобиля🚘", Telegram.Bot.Types.Enums.ParseMode.Default, false, false, 0, RemoveKeyboard);
                            lock (locker)
                            {
                                user.Money = text;
                                user.Status = Status.WaitForCarModel;
                            }
                            return;
                        }
                        if (user.Status == Status.WaitForCarModel)
                        {
                            lock (locker)
                            {
                                user.Car = text;
                                user.Status = Status.WaitMethod;
                            }
                            var keyboard = GetKeyboard(new List<string> { "Напишите мне", "Позвоните мне" });
                            await BotClient.SendTextMessageAsync(e.Message.Chat, "Очень хороший выбор, у вас есть вкус👍 Как бы вы хотели получить ответ?", Telegram.Bot.Types.Enums.ParseMode.Default, false, false, 0, keyboard);
                            return;
                        }
                        if (user.Status == Status.WaitMethod)
                        {
                            if (text == "Напишите мне")
                            {
                                lock (locker)
                                {
                                    user.Status = Status.WaitForPhoneNumber;
                                    user.ConnectMethod = "Написать";
                                }
                                await BotClient.SendTextMessageAsync(e.Message.Chat, "Тогда оставьте свои контакты и наши эксперты свяжуться с вами в  ближайшее время😊");
                            }
                            if (text == "Позвоните мне")
                            {
                                lock (locker)
                                {
                                    user.Status = Status.WaitForPhoneNumber;
                                    user.ConnectMethod = "Позвонить";
                                }
                                await BotClient.SendTextMessageAsync(e.Message.Chat, "Тогда оставьте свои контакты и наши эксперты свяжуться с вами в  ближайшее время😊");
                            }
                            return;
                        }
                        if (user.Status == Status.WaitForPhoneNumber)
                        {
                            //Users.Find(x => x.Id == user.Id).Phone = text;
                            lock (locker)
                            {
                                user = Users.FirstOrDefault(x => x.Id == user.Id);
                                user.Phone = text;
                                user.Status = Status.ready;
                            }
                            if (user != null)
                                Mail.send("Клиент из бота", user.GetString());
                            //TODO отправляем письмо
                            var RemoveKeyboard = new ReplyKeyboardRemove();
                            await BotClient.SendTextMessageAsync(e.Message.Chat, "Спасибо, с Вами свяжутся в ближайшие время. Хорошего Вам дня👍", Telegram.Bot.Types.Enums.ParseMode.Default, false, false, 0, RemoveKeyboard);
                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
                Reboot();
            }

        }
        public static ReplyKeyboardMarkup GetKeyboard(List<string> Buttons)
        {
            var buts = Buttons.Select(x => new KeyboardButton(x)).ToArray();
            var keyboard = new ReplyKeyboardMarkup();
            keyboard.ResizeKeyboard = true;
            keyboard.OneTimeKeyboard = true;
            keyboard.Keyboard = new KeyboardButton[][]
            {
               buts,
                 new KeyboardButton[]
                {
                    new KeyboardButton("Отмена")
                },
            };
            return keyboard;
        }
        private static InlineKeyboardButton[][] GetInlineKeyboard(Dictionary<string, string> CallKeyTextVal)
        {
            var keyboardInline = new InlineKeyboardButton[1][];
            var keyboardButtons = new InlineKeyboardButton[CallKeyTextVal.Count];
            foreach (var dic in CallKeyTextVal)
            {

            }
            for (var i = 0; i < CallKeyTextVal.Count; i++)
            {
                keyboardButtons[i] = new InlineKeyboardButton
                {
                    Text = CallKeyTextVal.Values.ToList()[i],
                    CallbackData = CallKeyTextVal.Keys.ToList()[i],
                };
            }
            keyboardInline[0] = keyboardButtons;
            return keyboardInline;
        }
        public static class Mail
        {
            public static MailAddress From { get; set; } = new MailAddress("Mail отправителя", "Телеграмм бот");
            public static MailAddress To { get; set; } = new MailAddress("Mail получателя");
            public static string Pass { get; set; } = "Пароль отправителя";

            public static void send(string Subject, string Body)
            {
                MailMessage msg = new MailMessage(From, To);
                msg.Subject = Subject;
                msg.Body = Body;
                msg.IsBodyHtml = false;
                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.Credentials = new System.Net.NetworkCredential("Mail отправителя", Pass);
                smtp.EnableSsl = true;
                smtp.Send(msg);
            }
        }
        public enum Status
        {
            ready,
            Started,
            WaitForPhoneNumber,
            WaitForCarModel,
            WaitMoney,
            WaitMethod,
        }
        public class User
        {
            public int Id { get; set; }
            public string Username { get; set; }
            public string Money { get; set; }
            public string Car { get; set; }
            public string Phone { get; set; }
            public string ConnectMethod { get; set; }
            public Status Status { get; set; } = Status.ready;
            public User() { }
            public string GetString()
            {
                return $"Telegram UserName: @{Username ?? "Не указан"}{Environment.NewLine}Бюджет: {Money ?? "Не указан"}{Environment.NewLine}Модель авто: {Car ?? "Не указан"}{Environment.NewLine}Способ связи: {ConnectMethod}{Environment.NewLine}Телефон: {Phone ?? "Не указан"}";
            }
        }
    }
}
