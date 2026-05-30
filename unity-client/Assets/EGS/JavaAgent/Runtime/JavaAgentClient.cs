using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EGS.JavaAgent.Runtime
{
    public sealed class JavaAgentClient
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task<AgentResponse> ExecuteAsync(string endpoint, AgentEnvelope envelope)
        {
            var json = JsonUtility.ToJson(envelope);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Java Agent call failed: {(int)response.StatusCode} ({response.ReasonPhrase})\n{responseJson}"
                );
            }

            return JsonUtility.FromJson<AgentResponse>(responseJson);
        }
    }
}
