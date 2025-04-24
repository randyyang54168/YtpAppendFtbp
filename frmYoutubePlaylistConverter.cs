using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YtpAppendFtbp
{
    public partial class frmYoutubePlaylistConverter : Form
    {
        private string inputFolderPath = "";
        private string apiKey = "";

        public frmYoutubePlaylistConverter()
        {
            InitializeComponent();
        }

        private void btnBrowseCsv_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                inputFolderPath = folderBrowserDialog.SelectedPath;
                txtFolderPath.Text = inputFolderPath;
            }
        }

        private async void btnConvert_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtYtApiKey.Text))
            {
                MessageBox.Show("Please input your Youtube API Key.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            apiKey = txtYtApiKey.Text;

            if (string.IsNullOrEmpty(inputFolderPath))
            {
                MessageBox.Show("Please first select the folder containing the CSV file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string[] csvFiles = Directory.GetFiles(inputFolderPath, "*.csv");

            if (csvFiles.Length == 0)
            {
                MessageBox.Show("No .csv archives were found in the selected folder.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "FreeTube Playlist db (*.db)|*.db|All files (*.*)|*.*";
            openFileDialog.Title = "Select the FreeTube playlist JSON archive to append to";
            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                MessageBox.Show("You must select a FreeTube playlist .db file to append。", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string existingJsonFilePath = openFileDialog.FileName;

            // Init ProgressBar
            if (progressBar != null)
            {
                progressBar.Maximum = csvFiles.Length;
                progressBar.Value = 0;
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = apiKey,
                ApplicationName = this.GetType().ToString()
            });

            foreach (string csvFile in csvFiles)
            {
                string playlistName = Path.GetFileNameWithoutExtension(csvFile);
                List<string> videoIds = new List<string>();

                // Show start convert
                if (listBoxLog != null)
                {
                    listBoxLog.Items.Add($"Start processing the archive: {playlistName}.csv");
                }

                try
                {
                    using (var reader = new StreamReader(csvFile))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            var values = line.Split(',');
                            if (values.Length > 0 && !string.IsNullOrWhiteSpace(values[0]))
                            {
                                videoIds.Add(values[0].Trim());
                            }
                        }
                    }

                    if (videoIds.Count == 0)
                    {
                        MessageBox.Show($"There is no valid Video ID in the file {playlistName}.csv, skipped.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (progressBar != null) progressBar.Value++;
                        continue;
                    }

                    var videosToAdd = new List<FreeTubeVideo>();
                    var parts = "snippet,contentDetails";
                    bool firstAppend = true;

                    for (int i = 0; i < videoIds.Count; i += 50)
                    {
                        var batchVideoIds = videoIds.Skip(i).Take(50).ToList();
                        var listRequest = youtubeService.Videos.List(parts);
                        listRequest.Id = string.Join(",", batchVideoIds);
                        var listResponse = await listRequest.ExecuteAsync();

                        foreach (var video in listResponse.Items)
                        {
                            videosToAdd.Add(new FreeTubeVideo
                            {
                                videoId = video.Id,
                                title = video.Snippet.Title,
                                author = video.Snippet.ChannelTitle,
                                authorId = video.Snippet.ChannelId,
                                lengthSeconds = ParseDuration(video.ContentDetails?.Duration),
                                published = ((DateTimeOffset)video.Snippet.PublishedAt).ToUnixTimeMilliseconds(),
                                timeAdded = ((DateTimeOffset)DateTime.Now).ToUniversalTime().ToUnixTimeMilliseconds(),
                                playlistItemId = Guid.NewGuid().ToString(),
                                type = "video"
                            });
                        }
                        await Task.Delay(1000);
                    }

                    if (videosToAdd.Count > 0)
                    {
                        try
                        {

                            var freeTubePlaylistToAdd = new FreeTubePlaylist
                            {
                                playlistName = playlistName,
                                @protected = false,
                                description = $"Imported from {playlistName}.csv",
                                videos = videosToAdd.Select(v => new FreeTubeVideo
                                {
                                    videoId = v.videoId,
                                    title = v.title,
                                    author = v.author,
                                    authorId = v.authorId,
                                    lengthSeconds = v.lengthSeconds,
                                    published = v.published,
                                    timeAdded = ((DateTimeOffset)DateTime.Now).ToUniversalTime().ToUnixTimeMilliseconds(),
                                    playlistItemId = Guid.NewGuid().ToString(),
                                    type = "video"
                                }).ToList(),
                                _id = playlistName.ToLower().Replace(" ", "_"),
                                createdAt = ((DateTimeOffset)DateTime.Now).ToUniversalTime().ToUnixTimeMilliseconds(),
                                lastUpdatedAt = ((DateTimeOffset)DateTime.Now).ToUniversalTime().ToUnixTimeMilliseconds()
                            };

                            string jsonOutputToAdd = JsonConvert.SerializeObject(freeTubePlaylistToAdd, Formatting.None);

                            // append to current json file
                            if (!firstAppend)
                            {
                                File.AppendAllText(existingJsonFilePath, Environment.NewLine + jsonOutputToAdd);
                            }
                            else
                            {
                                File.AppendAllText(existingJsonFilePath, jsonOutputToAdd);
                                firstAppend = false;
                            }


                            // Update UI
                            if (progressBar != null) progressBar.Value++;
                            if (listBoxLog != null) listBoxLog.Items.Add($"{videosToAdd.Count} new videos in {playlistName}.csv have been successfully append to {Path.GetFileName(existingJsonFilePath)}");

                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"An error occurred while append to an existing JSON file：{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            if (progressBar != null) progressBar.Value++;
                            if (listBoxLog != null) listBoxLog.Items.Add($"An error occurred while appending {playlistName}.csv：{ex.Message}");
                        }
                    }
                    else
                    {
                        MessageBox.Show($"No new videos were found in the archive {playlistName}.csv to append.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (progressBar != null) progressBar.Value++; 
                        if (listBoxLog != null) listBoxLog.Items.Add($"File {playlistName}.csv No new videos to append.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error occurred while processing file {playlistName}.csv: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (progressBar != null) progressBar.Value++;
                    if (listBoxLog != null) listBoxLog.Items.Add($"Error occurred while processing {playlistName}.csv: {ex.Message}");
                }
            }

            MessageBox.Show("All selected CSV files have been processed.", "Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Reset ProgressBar
            if (progressBar != null) progressBar.Value = 0;
        }

        // For creating FreeTube JSON format
        private class FreeTubePlaylist
        {
            public string playlistName { get; set; }
            public bool @protected { get; set; }
            public string description { get; set; }
            public List<FreeTubeVideo> videos { get; set; }
            public string _id { get; set; }
            public long createdAt { get; set; }
            public long lastUpdatedAt { get; set; }
        }

        // Used to create FreeTube video objects
        private class FreeTubeVideo
        {
            public string videoId { get; set; }
            public string title { get; set; }
            public string author { get; set; }
            public string authorId { get; set; }
            public long lengthSeconds { get; set; }
            public long published { get; set; }
            public long timeAdded { get; set; }
            public string playlistItemId { get; set; }
            public string type { get; set; }
        }

        // Parse ISO 8601 duration format (for example: PT2M31S) as seconds
        private long ParseDuration(string duration)
        {
            if (string.IsNullOrEmpty(duration)) return 0;

            TimeSpan timeSpan = System.Xml.XmlConvert.ToTimeSpan(duration);
            return (long)timeSpan.TotalSeconds;
        }


    }
}
