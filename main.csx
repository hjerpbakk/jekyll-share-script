#!/usr/bin/env dotnet-script
#r "nuget: YamlDotNet, 11.2.1"

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Net.Http;
using System.Text.RegularExpressions;

if (Args.Count != 1) {
    Console.WriteLine("Please provide the location of a Jekyll repository as an argument. Example: dotnet script main.csx -- path");
    return;
}

Console.WriteLine("Creating share text...");
var postPath = Path.GetFullPath(Path.Combine(Args[0], "_posts"));
var latestPostPath = Directory.EnumerateFileSystemEntries(postPath).OrderByDescending(p => p).FirstOrDefault();
if (latestPostPath == null) {
    Console.WriteLine("Could not find a single post at " + postPath);
    return;
}

var postPreamble = GetPreambleFromPost();
if (postPreamble == null) {
    Console.WriteLine(postPath + " did not contain a valid YML preamble.");
    return;
}

var preamble = ParsePreamble();
var hashtags = GetHashtags();
var postUrl = await GetPostUrl(preamble.date);
Console.WriteLine($"Blogged: {preamble.title} {postUrl} {string.Join(' ', hashtags)}");

string GetPreambleFromPost() {
    var fullText = File.ReadAllText(latestPostPath);
    var indexOfFirstLineBreak = fullText.IndexOf('\n');
    var indexOfPreambleEnd = fullText.IndexOf("---\n", indexOfFirstLineBreak, StringComparison.InvariantCulture);
    if (indexOfPreambleEnd == -1) {
        return null;
    }

    var preambleEnd = indexOfPreambleEnd + 3;
    var preambleText = fullText.Substring(0, preambleEnd).Trim('-');
    return preambleText;
}

Preamble ParsePreamble() {
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    return deserializer.Deserialize<Preamble>(postPreamble);
}

List<string> GetHashtags() {
    var tagsPath = Path.GetFullPath(Path.Combine(Args[0], "_my_tags"));
    var availableTags = Directory.EnumerateFileSystemEntries(tagsPath)
        .Where(f => f.EndsWith("md"))
        .Select(f => ParseTag(File.ReadAllText(f)))
        .ToDictionary(t => t.slug, t => t.hash_tag == null ? t.title : t.hash_tag);
    var hashtags = new List<string>(preamble.tags.Count);
    foreach (var tag in preamble.tags) {
        hashtags.Add("#" + availableTags[tag]);
    }

    return hashtags;
}

Tag ParseTag(string tagText) {
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    tagText = tagText.Trim('-');
    return deserializer.Deserialize<Tag>(tagText);
}

async Task<string> GetPostUrl(DateTime publishedDate) {
    var postFileName = Path.GetFileNameWithoutExtension(latestPostPath);
    var indexOfThirdDash = Regex.Matches(postFileName, "[-]")[2].Index;
    var name = postFileName.Substring(indexOfThirdDash + 1);

    var postUrl = new StringBuilder("https://hjerpbakk.com/blog");
    postUrl.Append('/');
    postUrl.Append(publishedDate.Year);
    postUrl.Append('/');
    postUrl.Append(publishedDate.Month.ToString("D2"));
    postUrl.Append('/');
    postUrl.Append(publishedDate.Day.ToString("D2"));
    postUrl.Append('/');
    postUrl.Append(name);

    var url = postUrl.ToString();
    Console.WriteLine("Verifying that " + url + " exists...");
    await VerifyUrl(url);
    return url;
}

async Task VerifyUrl(string url) {
    using (var httpClient = new HttpClient()) {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}

struct Preamble {
    public string title { get; set; }
    public List<string> tags { get; set; }

    public DateTime date { get; set; }
}

struct Tag {
    public string slug { get; set; }
    public string title { get; set; }
    public string hash_tag { get; set; }
}