using System;
using System.Collections.Generic;

namespace InfinitePickaxe.Client.Metadata
{
    public sealed class MailMetaResolver
    {
        private readonly Dictionary<uint, MailTemplateMeta> templatesById = new Dictionary<uint, MailTemplateMeta>();
        private readonly List<MailTemplateMeta> templates = new List<MailTemplateMeta>();
        private bool initialized;
        private bool warnedNoMeta;

        public MailMetaResolver()
        {
            InitializeFromMeta();
        }

        public IReadOnlyList<MailTemplateMeta> Templates => templates;
        public uint MaxMailCount { get; private set; }
        public uint ExpireDays { get; private set; }
        public uint ClaimAllLimit { get; private set; }
        public uint DefaultListLimit { get; private set; }
        public bool HasData => templates.Count > 0;

        public bool TryGetTemplate(uint templateId, out MailTemplateMeta meta)
        {
            return templatesById.TryGetValue(templateId, out meta);
        }

        public void Reload()
        {
            initialized = false;
            warnedNoMeta = false;
            templatesById.Clear();
            templates.Clear();
            MaxMailCount = 0;
            ExpireDays = 0;
            ClaimAllLimit = 0;
            DefaultListLimit = 0;
            InitializeFromMeta();
        }

        private void InitializeFromMeta()
        {
            if (initialized) return;
            initialized = true;

            if (!MetaRepository.Loaded || MetaRepository.Data == null)
            {
                return;
            }

            if (!MetaRepository.Data.TryGetValue("mail", out var obj) || obj is not Dictionary<string, object> dict)
            {
                if (!warnedNoMeta)
                {
                    warnedNoMeta = true;
                    UnityEngine.Debug.LogWarning("MailMetaResolver: mail section missing in meta_bundle.json.");
                }
                return;
            }

            if (TryGetUInt(dict, out var maxMailCount, "max_mail_count"))
            {
                MaxMailCount = maxMailCount;
            }

            if (TryGetUInt(dict, out var expireDays, "expire_days"))
            {
                ExpireDays = expireDays;
            }

            if (TryGetUInt(dict, out var claimAllLimit, "claim_all_limit"))
            {
                ClaimAllLimit = claimAllLimit;
            }

            if (TryGetUInt(dict, out var defaultListLimit, "default_list_limit"))
            {
                DefaultListLimit = defaultListLimit;
            }

            if (dict.TryGetValue("templates", out var templatesObj) && templatesObj is List<object> templateList)
            {
                foreach (var entry in templateList)
                {
                    if (entry is not Dictionary<string, object> templateDict) continue;

                    if (!TryGetUInt(templateDict, out var templateId, "template_id"))
                    {
                        continue;
                    }

                    var meta = new MailTemplateMeta
                    {
                        TemplateId = templateId,
                        MailType = TryGetString(templateDict, out var mailType, "mail_type") ? mailType : string.Empty,
                        Title = TryGetString(templateDict, out var title, "title") ? title : string.Empty,
                        Body = TryGetString(templateDict, out var body, "body") ? body : string.Empty,
                        Sender = TryGetString(templateDict, out var sender, "sender") ? sender : string.Empty
                    };

                    var defaultExpireDays = ExpireDays;
                    if (TryGetUInt(templateDict, out var templateExpire, "default_expire_days"))
                    {
                        defaultExpireDays = templateExpire;
                    }

                    meta.DefaultExpireDays = defaultExpireDays;

                    templatesById[templateId] = meta;
                    templates.Add(meta);
                }
            }
        }

        private static bool TryGetString(Dictionary<string, object> dict, out string value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && obj != null)
                {
                    value = obj.ToString();
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static bool TryGetUInt(Dictionary<string, object> dict, out uint value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.TryGetValue(key, out var obj) && TryConvertToUInt(obj, out value))
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private static bool TryConvertToUInt(object obj, out uint value)
        {
            switch (obj)
            {
                case uint u:
                    value = u;
                    return true;
                case int i when i >= 0:
                    value = (uint)i;
                    return true;
                case long l when l >= 0:
                    value = (uint)Math.Min(l, uint.MaxValue);
                    return true;
                case ulong ul:
                    value = (uint)Math.Min(ul, uint.MaxValue);
                    return true;
                case double d when d >= 0:
                    value = (uint)d;
                    return true;
                case float f when f >= 0:
                    value = (uint)f;
                    return true;
                case string s when uint.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
            }

            value = 0;
            return false;
        }
    }

    public sealed class MailTemplateMeta
    {
        public uint TemplateId { get; set; }
        public string MailType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public uint DefaultExpireDays { get; set; }
    }
}
