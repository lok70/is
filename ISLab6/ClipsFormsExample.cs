using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CLIPSNET;
using Newtonsoft.Json;

namespace ClipsFormsExample
{
    public class FactDefinition { public string id { get; set; } public string text { get; set; } }

    public class DialogVariant { public string text { get; set; } public string react { get; set; } }

    public class DialogDefinition
    {
        public string id { get; set; }
        public List<string> conditions { get; set; }
        public string target { get; set; }
        public string message { get; set; }
        public List<DialogVariant> variants { get; set; }
    }

    public class FactList { public List<FactDefinition> facts { get; set; } }
    public class RuleDefinition { public string id { get; set; } public List<string> conditions { get; set; } public string conclusion { get; set; } }
    public class RuleList { public List<RuleDefinition> rules { get; set; } }
    public class DialogList { public List<DialogDefinition> dialogs { get; set; } }

    public partial class ClipsFormsExample : Form
    {
        private CLIPSNET.Environment clips = new CLIPSNET.Environment();
        private List<FactDefinition> allFacts;
        private List<RuleDefinition> allRules;
        private List<DialogDefinition> allDialogs;

        public ClipsFormsExample()
        {
            InitializeComponent();
            SetupGrid();
            LoadKnowledgeBase();
        }

        private void SetupGrid()
        {
            factsGrid.Columns.Clear();
            factsGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Выбор", Name = "Selected", Width = 50 });
            factsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", Name = "ID", ReadOnly = true, Width = 60 });
            factsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Симптом", Name = "Text", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        }

        private void LoadKnowledgeBase()
        {
            try
            {
                if (File.Exists("facts.json"))
                    allFacts = JsonConvert.DeserializeObject<FactList>(File.ReadAllText("facts.json", Encoding.UTF8)).facts;

                if (File.Exists("rules.json"))
                    allRules = JsonConvert.DeserializeObject<RuleList>(File.ReadAllText("rules.json", Encoding.UTF8)).rules;

                if (File.Exists("dialogs.json"))
                    allDialogs = JsonConvert.DeserializeObject<DialogList>(File.ReadAllText("dialogs.json", Encoding.UTF8)).dialogs;
                else
                    allDialogs = new List<DialogDefinition>();

                if (allFacts != null)
                {
                    factsGrid.Rows.Clear();
                    foreach (var fact in allFacts)
                        if (string.Compare(fact.id, "F046") < 0)
                            factsGrid.Rows.Add(false, fact.id, fact.text);
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки: " + ex.Message); }
        }

        private void nextBtn_Click(object sender, EventArgs e)
        {
            outputBox.Clear();
            buttonsPanel.Controls.Clear();
            clips.Clear();

            string generatedClips = GenerateClipsCode();
            clips.LoadFromString(generatedClips);

            if (clipsOpenFileDialog.FileNames.Length > 0)
                foreach (string file in clipsOpenFileDialog.FileNames) clips.Load(file);

            clips.Reset();

            outputBox.AppendText("Исходные данные:\r\n");
            foreach (DataGridViewRow row in factsGrid.Rows)
            {
                if (Convert.ToBoolean(row.Cells["Selected"].Value))
                {
                    string id = row.Cells["ID"].Value.ToString();
                    clips.Eval($"(assert (fact (id {id})))");
                    outputBox.AppendText($"- {id}\r\n");
                }
            }
            outputBox.AppendText("\r\n--- Старт анализа ---\r\n");
            RunInferenceLoop();
        }

        private void RunInferenceLoop()
        {
            clips.Run();
            HandleInteractiveDialogue();
        }

        private void HandleInteractiveDialogue()
        {
            var evalStr = "(find-fact ((?f ioproxy)) TRUE)";
            var result = clips.Eval(evalStr);

            if (result is MultifieldValue mf && mf.Count > 0)
            {
                FactAddressValue fv = (FactAddressValue)mf[0];
                string questionId = "";

                try { questionId = ((LexemeValue)fv["question-id"]).Value; } catch { }

                var dialogData = allDialogs.FirstOrDefault(d => d.id == questionId);

                if (dialogData != null)
                {
                    outputBox.AppendText($"\r\n[СИСТЕМА]: {dialogData.message}\r\n");
                    buttonsPanel.Controls.Clear();

                    foreach (var variant in dialogData.variants)
                    {
                        Button btn = new Button { Text = variant.text, AutoSize = true, Padding = new Padding(5) };
                        btn.Tag = new { ReactId = variant.react, QId = questionId };
                        btn.Click += OnDialogButtonClick;
                        buttonsPanel.Controls.Add(btn);
                    }
                }
            }
            else
            {
                ShowConclusions();
            }
        }

        private void OnDialogButtonClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            dynamic tag = btn.Tag;
            string reactId = tag.ReactId;
            string questionId = tag.QId;

            outputBox.AppendText($"[ВЫ]: {btn.Text}\r\n");
            buttonsPanel.Controls.Clear();

            if (!string.IsNullOrEmpty(questionId))
                clips.Eval($"(do-for-all-facts ((?f ioproxy)) (eq ?f:question-id {questionId}) (retract ?f))");

            if (!string.IsNullOrEmpty(reactId) && reactId != "none" && reactId != "nil")
            {
                clips.Eval($"(assert (fact (id {reactId})))");
                outputBox.AppendText($"-> Принят факт: {reactId}\r\n");
            }

            RunInferenceLoop();
        }

        private string GenerateClipsCode()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("(deftemplate fact (slot id) (slot text))");
            sb.AppendLine("(deftemplate ioproxy (slot question-id) (multislot messages) (multislot answers-text) (multislot answers-react))");

            if (allDialogs != null)
            {
                foreach (var diag in allDialogs)
                {
                    sb.AppendLine($"(defrule ask-{diag.id}");
                    foreach (var cond in diag.conditions) sb.AppendLine($"   (fact (id {cond}))");

                    sb.AppendLine($"   (not (fact (id {diag.target})))");
                    sb.AppendLine($"   (not (fact (id {diag.target}_asked)))");
                    sb.AppendLine("   =>");
                    sb.AppendLine($"   (assert (ioproxy (question-id {diag.id})");
                    sb.AppendLine($"                    (messages \"dummy\")");
                    sb.AppendLine($"                    (answers-text \"dummy\")");
                    sb.AppendLine($"                    (answers-react \"dummy\")))");
                    sb.AppendLine($"   (assert (fact (id {diag.target}_asked)))");
                    sb.AppendLine(")");
                }
            }

            if (allRules != null)
            {
                foreach (var rule in allRules)
                {
                    sb.AppendLine($"(defrule {rule.id}");
                    foreach (var condId in rule.conditions) sb.AppendLine($"   (fact (id {condId}))");
                    sb.AppendLine("   =>");
                    sb.AppendLine($"   (assert (fact (id {rule.conclusion}) (text \"dummy\")))");
                    sb.AppendLine(")");
                }
            }
            return sb.ToString();
        }

        private void ShowConclusions()
        {
            var evalStr = "(find-all-facts ((?f fact)) (neq ?f:text nil))";
            var result = clips.Eval(evalStr);

            if (result is MultifieldValue mf && mf.Count > 0)
            {
                outputBox.AppendText("\r\n=== РЕЗУЛЬТАТЫ ===\r\n");
                var list = new List<dynamic>();
                for (int i = 0; i < mf.Count; i++)
                {
                    FactAddressValue fv = (FactAddressValue)mf[i];
                    string id = ((LexemeValue)fv["id"]).Value;

                    var originalFact = allFacts.FirstOrDefault(f => f.id == id);
                    string text = originalFact != null ? originalFact.text : "Неизвестный факт";

                    list.Add(new { Id = id, Text = text });
                }

                foreach (var item in list.OrderBy(x => x.Id))
                {
                    string type = "[Вывод]";
                    if (string.Compare(item.Id, "F058") >= 0) type = "[РЕКОМЕНДАЦИЯ]";
                    else if (string.Compare(item.Id, "F053") >= 0) type = "[ЗАКЛЮЧЕНИЕ]";
                    outputBox.AppendText($"{type} {item.Id}: {item.Text}\r\n");
                }
            }
            else outputBox.AppendText("Нет выводов.\r\n");
        }

        private void resetBtn_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow r in factsGrid.Rows) r.Cells["Selected"].Value = false;
            outputBox.Clear();
            buttonsPanel.Controls.Clear();
            clips.Clear();
        }

        private void openFile_Click(object sender, EventArgs e) => clipsOpenFileDialog.ShowDialog();
    }
}