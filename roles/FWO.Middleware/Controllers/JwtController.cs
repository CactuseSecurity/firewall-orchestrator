using FWO.Api.Data;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Middleware.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class JwtController : ControllerBase
    {
        private readonly JwtWriter jwtWriter;

        // GET: api/<JwtController>
        [AllowAnonymous]
        [HttpGet]
        public async Task<string> GetAsync([FromBody] string username, [FromBody] string password)
        {
            UiUser user = new UiUser { Name = username, Password = password };
            
            return await jwtWriter.CreateJWT(user);
        }

        // GET api/<JwtController>
        [HttpGet]
        public async Task<string> GetEmptyAsync()
        {
            return await jwtWriter.CreateJWT();
        }

        // POST api/<JwtController>
        [HttpPost]
        public void Post([FromBody] string value)
        {

        }

        // PUT api/<JwtController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {

        }

        // DELETE api/<JwtController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {

        }
    }
}
