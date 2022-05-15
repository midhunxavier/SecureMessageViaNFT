
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VEDriversLite;
using VEDriversLite.Admin.Dto;
using VEDriversLite.Dto;
using VEDriversLite.NFT;
using VEDriversLite.NFT.Dto;
using VEDriversLite.Security;

namespace VENFT_WebAPI_Test
{
    public class CoreService : BackgroundService
    {
        //private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfiguration settings;
        private IHostApplicationLifetime lifetime;

        public CoreService(IConfiguration settings, IHostApplicationLifetime lifetime)
        {
            this.settings = settings; //startup configuration in appsettings.json
            this.lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
            await Task.Delay(1);

            //MainDataContext.IpfsSecret = settings.GetValue<string>("IpfsSecret", string.Empty);
            //MainDataContext.IpfsProjectID = settings.GetValue<string>("IpfsProjectID", string.Empty);
            //if (!string.IsNullOrEmpty(MainDataContext.IpfsSecret) && !string.IsNullOrEmpty(MainDataContext.IpfsProjectID))
            //    NFTHelpers.LoadConnectionInfo(MainDataContext.IpfsProjectID, MainDataContext.IpfsSecret);

            var acc = new List<string>();
            settings.GetSection("ObservedAccounts").Bind(acc);
            if (acc != null)
                MainDataContext.ObservedAccounts = acc;

            try
            {
                /*
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys.json");
                if (!FileHelpers.IsFileExists(path))
                {
                    Console.WriteLine("Missing keys.json file. Cannot continue without at least one root Neblio account.");
                    return;
                }
                var skeys = FileHelpers.ReadTextFromFile(path);
                var keys = JsonConvert.DeserializeObject<List<AccountExportDto>>(skeys);
                */
                var keys = new List<AccountExportDto>();
                settings.GetSection("keys").Bind(keys);
                if (keys == null || keys.Count == 0)
                {
                    //log.Error("Missing keys in settigns. Cannot continue without at least one root Neblio account.");
                    Console.WriteLine("Missing keys in settigns. Cannot continue without at least one root Neblio account.");
                    return;
                }

                Console.WriteLine("------------------------------------");
                Console.WriteLine("-----------Loading Accounts---------");
                Console.WriteLine("------------------------------------");

                if (keys != null)
                {
                    foreach (var k in keys)
                    {
                        if (!string.IsNullOrEmpty(k.Address))
                        {
                                var add = NeblioTransactionHelpers.ValidateNeblioAddress(k.Address);
                                if (!string.IsNullOrEmpty(add))
                                {
                                    Console.WriteLine("");
                                    Console.WriteLine("=========Neblio Main Account========");
                                    Console.WriteLine($"Loading Neblio address {k.Address}...");
                                    if (string.IsNullOrEmpty(k.Name))
                                        k.Name = NeblioTransactionHelpers.ShortenAddress(k.Address);

                                    var account = new NeblioAccount();
                                    EncryptedBackupDto bckp = null;
                                    if (FileHelpers.IsFileExists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, k.Address + "-backup.json")))
                                    {
                                        var b = FileHelpers.ReadTextFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, k.Address + "-backup.json"));
                                        if (!string.IsNullOrEmpty(b))
                                        {
                                            bckp = JsonConvert.DeserializeObject<EncryptedBackupDto>(b);
                                            Console.WriteLine($"Backup data found for this account. It will be loaded based on VENFT backup.");
                                        }
                                    }

                                    (bool, string) dbackup = (false, string.Empty);
                                    (bool, string) dpass = (false, string.Empty);
                                    if (await account.LoadAccount(k.Password, k.EKey, k.Address)) // fill your password
                                    {

                                        if (bckp != null)
                                        {
                                            var dadd = await ECDSAProvider.DecryptMessage(bckp.eadd, account.Secret);
                                            dbackup = await ECDSAProvider.DecryptMessage(bckp.edata, account.Secret);
                                            dpass = await ECDSAProvider.DecryptMessage(bckp.epass, account.Secret);
                                            if (dpass.Item1 && dadd.Item1 && dadd.Item1)
                                            {
                                                Console.WriteLine($"Loading Neblio address {k.Address} from VENFT Backup...");
                                                if (await account.LoadAccountFromVENFTBackup(dpass.Item2, dbackup.Item2))
                                                    Console.WriteLine($"Neblio address {k.Address} initialized.");
                                                else
                                                    Console.WriteLine($"Cannot load VENFT backup for address {k.Address}.");
                                            }
                                        }

                                        VEDLDataContext.Accounts.TryAdd(k.Address, account);
                                        VEDLDataContext.AdminAddresses.Add(k.Address);

                                    }
                                }
                            }
                        }
                    }
            }
            catch (Exception ex)
            {
                //log.Error("Canno load the keys. " + ex.Message);
                Console.WriteLine("Cannot load the keys. " + ex.Message);
            }

            await Task.Delay(5000);

            Console.WriteLine("------------------------------------");
            Console.WriteLine("Loading NFT Hashes...");
            await AccountHandler.ReloadNFTHashes();
            Console.WriteLine($"Count of NFTHashes: {VEDLDataContext.NFTHashs.Count}.");

            Console.WriteLine("------------------------------------");
            Console.WriteLine("--------Starting main cycle---------");
            Console.WriteLine("------------------------------------");
            try
            {
                var VENFTOwnersRefreshDefault = 3600;
                var VENFTOwnersRefresh = 100;
                var NFTHashsRefreshDefault = 100;
                var NFTHashsRefresh = 10;


                _ = Task.Run(async () =>
                {
                    Console.WriteLine("Loading Observing addresses...");
                    // load observed addresses
                    if (MainDataContext.ObservedAccounts.Count > 0)
                    {
                        for (var i = 0; i < MainDataContext.ObservedAccounts.Count; i++)
                        {
                            try
                            {
                                var tab = new VEDriversLite.Bookmarks.ActiveTab(MainDataContext.ObservedAccounts[i]);
                                tab.MaxLoadedNFTItems = 1500;
                                await tab.StartRefreshing(10000, false, true);
                                MainDataContext.ObservedAccountsTabs.TryAdd(tab.Address, tab);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Cannot start observing of account: " + MainDataContext.ObservedAccounts[i]);
                            }
                        }
                    }

                    Console.WriteLine("Running...");
                    while (!stopToken.IsCancellationRequested)
                    {

                        await Task.Delay(1000);

                        try
                        {
                            if (NFTHashsRefresh <= 0)
                            {
                                if (await AccountHandler.ReloadNFTHashes())
                                    NFTHashsRefresh = NFTHashsRefreshDefault;
                            }
                            else
                            {
                                NFTHashsRefresh--;
                            }
                        }
                        catch (Exception ex)
                        {
                            //log.Error("Cannot reload the NFT Hashes. " + ex.Message);
                        }
                    }
                });

            }
            catch (Exception ex)
            {
                //log.Error("Fatal error in main loop. " + ex.Message);
                lifetime.StopApplication();
            }

        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }
    }
}
