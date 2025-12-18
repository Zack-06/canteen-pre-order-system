using System.Security.Cryptography;
using WebPush;

namespace Superchef.Services;

public class GeneratorHelper
{
    private static char RandomChar(string source)
    {
        // RandomNumberGenerator.GetInt32(3) returns a random number between 0 and 2
        return source[RandomNumberGenerator.GetInt32(source.Length)];
    }

    public static string RandomString(int length, string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz")
    {
        if (length < 1)
        {
            length = 1;
        }

        string tmpStr = "";
        for (int i = 0; i < length; i++)
        {
            tmpStr += RandomChar(chars);
        }

        return tmpStr;
    }

    public static string RandomPassword(int length)
    {
        if (length < 6)
        {
            length = 6;
        }

        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string special = "!@#$%^&*()_+-=";
        const string all = lower + upper + digits + special;

        var password = new char[length];

        // Guarantee password contains at least one lower, one upper, one digit, and one special character
        password[0] = RandomChar(lower);
        password[1] = RandomChar(upper);
        password[2] = RandomChar(digits);
        password[3] = RandomChar(special);

        // Generate remaining characters randomly
        for (int i = 4; i < length; i++)
        {
            password[i] = RandomChar(all);
        }

        // Shuffle (fisher yates)
        // https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
        for (int i = password.Length - 1; i >= 1; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    public static string RandomReadyMessage(string storeName)
    {
        List<string> messages = [
            $"Still here! Your meal at {storeName} is ready and waiting for your arrival. 🕒",
            $"Don't leave it hanging! Your food from {storeName} is sitting pretty at the counter. 🍱",
            $"Your stomach is calling! Your order at {storeName} has been ready for a while now. 📢",
            $"Quick heads up! Your order from {storeName} is all set and waiting for you to collect. 🏃‍♂️",
            $"Food's getting lonely! Your order is ready at {storeName}, come and get it! 🤗",
            $"Hungry? Your food from {storeName} is ready and waiting to be picked up! 🥨",
            $"Don't let it get cold! Your order from {storeName} is still waiting for you. ❄️"
        ];

        return messages[RandomNumberGenerator.GetInt32(messages.Count)];
    }

    // generate a new key
    public static void GenerateVapidKey()
    {
        var keys = VapidHelper.GenerateVapidKeys();
        Console.WriteLine($"VAPID_PUBLIC_KEY={keys.PublicKey}");
        Console.WriteLine($"VAPID_PRIVATE_KEY={keys.PrivateKey}");
    }
}