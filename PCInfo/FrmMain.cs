using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Text;
using System.Windows.Forms;
using PCInfo.Properties;

namespace PCInfo
{
    public partial class FrmMain : Form
    {
        public FrmMain()
        {
            InitializeComponent();
        }

        private void FrmMain_Shown(object sender, EventArgs e)
        {
            txtDetails.Clear();

            var builder = new StringBuilder(string.Format(Resources.HostName, Dns.GetHostName()));
            builder.Append(Environment.NewLine);

            builder.Append(string.Format(Resources.MachineName, Environment.MachineName));
            builder.Append(Environment.NewLine);

            builder.Append(string.Format(Resources.UserDomainName, Environment.UserDomainName));
            builder.Append(Environment.NewLine);

            // Forest and Domains
            var forest = Forest.GetCurrentForest();
            builder.Append(Environment.NewLine);
            builder.Append(string.Format(Resources.Forest, forest.Name)).Append(Environment.NewLine);

            var domains = forest.Domains;
            builder.Append(Environment.NewLine).Append(Resources.ForestDomains).Append(Environment.NewLine);
            foreach (Domain domain in domains)
            {
                builder.Append(domain.Name);
                builder.Append(", ");
            }
            builder.Append(Environment.NewLine);

            // Domain related
            var currDomain = Domain.GetCurrentDomain();

            builder.Append(Environment.NewLine);
            builder.Append(string.Format(Resources.DomainName, currDomain.Name));
            builder.Append(Environment.NewLine);

            using (var context = new PrincipalContext(ContextType.Domain))
            {
                var server = context.ConnectedServer; // "dc1.domain.com"
                var splitted = server.Split('.'); // { "dc1", "domain", "com" }
                var formatted = splitted.Select(s => string.Format("DC={0}", s)); // { "DC=dc1", "DC=domain", "DC=com" }
                string.Join(",", formatted);

                builder.Append(string.Format(Resources.CurrentController, server)).Append(Environment.NewLine);
            }

            // Domain Controllers
            builder.Append(Environment.NewLine).Append(Resources.DC).Append(Environment.NewLine);

            var dcs = GetDomainControllers(currDomain.GetDirectoryEntry());
            foreach (var dc in dcs)
            {
                builder.Append(dc);
                builder.Append(", ");
            }

            // All details in textbox
            txtDetails.Text = builder.ToString().TrimEnd(", ".ToCharArray());
        }

        /// <summary>
        /// Get dictionary of domain controllers with their readable or writable status.
        /// </summary>
        /// <param name="domainEntry">The domain entry representing domain.</param>
        /// <returns>Return dictionary object</returns>
        private static List<string> GetDomainControllers(DirectoryEntry domainEntry)
        {
            const string aName = "dNSHostName";
            const string aPrimGroupId = "primaryGroupID";
            var dcs = new List<string>();

            // Search all DCs (both readable (521) and writable (516)
            var searcher = new DirectorySearcher(domainEntry)
            {
                Filter = "(&(objectCategory=computer)(objectClass=computer)(|((primaryGroupID=516)(primaryGroupID=521))))"
            };
            searcher.PropertiesToLoad.AddRange(new[] { aName,aPrimGroupId });
            var results = searcher.FindAll();

            foreach (SearchResult result in results)
            {
                // To see all attribute values in Output window
                //int counter = 1;
                //DirectoryEntry entry = result.GetDirectoryEntry();
                //foreach (PropertyValueCollection property in entry.Properties)
                //    foreach (var value in from object o in property select o.ToString())
                //    {
                //        var info = string.Format("{0}. Property: {1} and Value: {2}", counter,property.PropertyName, value);
                //        Debug.WriteLine(info);
                //        counter++;
                //    }

                var name = Convert.ToString(result.Properties[aName][0]);
                var pgId = Convert.ToString(result.Properties[aPrimGroupId][0]);

                int primaryGroupID;
                if (int.TryParse(pgId, out primaryGroupID))
                {
                    // RID for the "Read-only Domain Controllers" built-in group in Active Directory
                    // Writable Domain Controllers have primaryGroupID set to 516 (the "Domain Controllers" group).
                    name = string.Format(primaryGroupID == 521 ? "{0} (Read only)" : "{0}", name);
                }

                dcs.Add(name);
            }

            return dcs;
        }
    }
}
