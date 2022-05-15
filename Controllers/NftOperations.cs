using Microsoft.AspNetCore.Mvc;
using System.Net;
using VEDriversLite;
using VEDriversLite.NFT;
using VEDriversLite.Security;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace VENFT_WebAPI_Test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NftOperations : ControllerBase
    {
        private static NeblioAccount account = new NeblioAccount();


        // GET api/<NftOperations>/1
        // @param txid
        // returns NFT info
        [HttpGet("GetNFT/{txid}")]
        public async Task<INFT> GetAsync(string txid)
        {
            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, txid, 0);

            return nft;
        }

        
        // POST api/<NftOperations>
        [HttpPost]
        [Route("SendNFTMessage")]
        public async Task<string> SendNFTMessage([FromBody] string param)
        {
            var split = param.Split(new[] { ','}, StringSplitOptions.RemoveEmptyEntries);

            var receiverAddress = split[0];

            var message = split[1];

            var name = "Device1";

            var myaccountAddress = VEDriversLite.VEDLDataContext.AdminAddresses.FirstOrDefault();

            if (string.IsNullOrEmpty(myaccountAddress)) return "No admin address found";

            if (VEDriversLite.VEDLDataContext.Accounts.TryGetValue(myaccountAddress, out var account))
            {

                var signature = await account.SignMessage(message);

                var encryptedMessage = await ECDSAProvider.EncryptStringWithSharedSecret(message, receiverAddress, account.Secret);

                var res = await account.SendMessageNFT(name, encryptedMessage.Item2, receiverAddress, "", false, "", "", signature.Item2, "");

                //var res = await account.SendMessageNFT(name, message, receiverAddress, "", true, "", "", "", "");

                return res.Item2;
            }
            else
                return "Cannot find the account.";
        }

        // POST api/<NftOperations>
        [HttpPost]
        [Route("VerifyNFTMessage")]
        public async Task<string> VerifyDecryptNFTMessage([FromBody] string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            var txid = split[0];

            var address = split[1];

            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, txid, 0);

            var sender = await VEDriversLite.NeblioTransactionHelpers.GetTransactionSender(nft.Utxo, nft.TxDetails);

            if (sender == address) return "Verified";

            return "Not Verified";
        }

        // POST api/<NftOperations>
        [HttpPost]
        [Route("DecryptNFTMessage")]
        public async Task<string> DecryptNFTMessage([FromBody] string param)
        {
            var split = param.Split(new[] { ','}, StringSplitOptions.RemoveEmptyEntries);

            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, split[0], 0);

            var myaccountAddress = VEDriversLite.VEDLDataContext.AdminAddresses.FirstOrDefault();

            if (string.IsNullOrEmpty(myaccountAddress)) return "No admin address found";


            if (VEDriversLite.VEDLDataContext.Accounts.TryGetValue(myaccountAddress, out var account))
            {
                var res = await ECDSAProvider.DecryptStringWithSharedSecret(nft.Description, split[1], account.Secret);
                return res.Item2;

            }
            else
                return "Cannot find the account.";


        }

    }
}
