﻿using Common.Models;
using Service;

namespace Logic
{
    public class Bep20Logic : IBep20Logic
    {
        private readonly IBscScanApiService _bscScanApiService;

        public Bep20Logic(IBscScanApiService bscScanApiService)
        {
            _bscScanApiService = bscScanApiService;
        }

        public async Task<Bep20TokenTransactionResponse> GetBEP20TokenTransactions(Bep20TokenTransactionRequest request)
        {
            if(request.Address == null || request.Address.Length == 0)
            {
                throw new ArgumentException("Address cannot be null or empty");
            }

            if (request.BEP20TokenContract == null || request.BEP20TokenContract.Length == 0)
            {
                throw new ArgumentException("BEP20TokenContract cannot be null or empty");
            }

            var addressesToIgnore = request.IgnoreAddresses?
                .Select(ignoreAddress => ignoreAddress.Address).OfType<string>()
                .ToList() ?? new List<string>();

            var intialAddressTransactions = await GetTransactionsForAddress(request.Address, request.BEP20TokenContract);

            addressesToIgnore.Add(request.Address);

            var distinctAddressesOutgoingTransactions = intialAddressTransactions
                .Where(transaction => addressesToIgnore.All(addressNotToProcess => addressNotToProcess != transaction.To))
                .Select(transaction => transaction.To)
                .OfType<string>()
                .Distinct()
                .ToList();

            var subAddressTransactions = new List<Bep20TokenTransactions>();

            var initialDepth = 1;
            await GetSubAddressTransactions(request.BEP20TokenContract, distinctAddressesOutgoingTransactions, addressesToIgnore,
                subAddressTransactions, request.ExplorationDepth, initialDepth);

            return new Bep20TokenTransactionResponse(request.Address, intialAddressTransactions, subAddressTransactions);
        }

        public async Task<List<BscScanTokenTransfer>> GetTransactionsForAddress(string address, string contract)
        {
            var page = 1;
            var transactions = new List<BscScanTokenTransfer>();
            BscScanResult<List<BscScanTokenTransfer>>? bscScanResult;

            do
            {
                bscScanResult = await _bscScanApiService.GetTokenTransferEventsForAddress(address, 1000, page, contract);

                if (bscScanResult == null || bscScanResult.Status == null)
                {
                    return transactions;
                }

                transactions.AddRange(bscScanResult?.Result ?? new List<BscScanTokenTransfer>());

                page++;
            } while (bscScanResult?.Result?.Count > 0);

            return transactions;
        }

        private async Task GetSubAddressTransactions(string contract, List<string> addressesForLookup, List<string> addressesToIgnore, 
            List<Bep20TokenTransactions> subAddressTransactions, int maxExplorationDepth, int depth)
        {
            if (depth > maxExplorationDepth || depth == 0)
            {
                return;
            }

            addressesToIgnore.AddRange(addressesForLookup);
            addressesToIgnore = addressesToIgnore.Distinct().ToList();

            foreach (var subAddress in addressesForLookup)
            {
                var transactions = await GetTransactionsForAddress(subAddress, contract);
                subAddressTransactions.Add(new Bep20TokenTransactions(subAddress, depth, transactions));
                addressesToIgnore.Add(subAddress);

                var subDistinctAddressesOutgoingTransactions = transactions
                    .Where(transaction => addressesToIgnore.All(addressNotToProcess => addressNotToProcess != transaction.To))
                    .Select(transaction => transaction.To)
                    .OfType<string>()
                    .Distinct()
                    .ToList();

                if (subDistinctAddressesOutgoingTransactions.Any())
                {
                    await GetSubAddressTransactions(contract, subDistinctAddressesOutgoingTransactions, addressesToIgnore,
                        subAddressTransactions, maxExplorationDepth, depth + 1); ;
                }
            }
        }

        public Task<Bep20TokenTransactionResponseSimplified> GetBEP20TokenTransactionsSimplified(Bep20TokenTransactionRequest request)
        {
            throw new NotImplementedException();
        }
    }
}