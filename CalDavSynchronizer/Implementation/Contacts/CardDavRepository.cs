// This file is Part of CalDavSynchronizer (http://outlookcaldavsynchronizer.sourceforge.net/)
// Copyright (c) 2015 Gerhard Zehetbauer
// Copyright (c) 2015 Alexander Nimmervoll
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CalDavSynchronizer.DataAccess;
using CalDavSynchronizer.Diagnostics;
using GenSync;
using GenSync.EntityRepositories;
using GenSync.Logging;
using log4net;
using Thought.vCards;
using CalDavSynchronizer.ThoughtvCardWorkaround;
using CalDavSynchronizer.Utilities;

namespace CalDavSynchronizer.Implementation.Contacts
{
  public class CardDavRepository : CardDavEntityRepository<vCard, vCardStandardReader>
  {
    private static readonly ILog s_logger = LogManager.GetLogger(MethodInfo.GetCurrentMethod().DeclaringType);
    private readonly vCardImprovedWriter _vCardImprovedWriter;

    public CardDavRepository(ICardDavDataAccess cardDavDataAccess, IChunkedExecutor chunkedExecutor) : base(cardDavDataAccess, chunkedExecutor)
    {
      _vCardImprovedWriter = new vCardImprovedWriter();
    }

    protected override void SetUid(vCard entity, string uid)
    {
      entity.UniqueId = uid;
    }

    protected override string GetUid(vCard entity)
    {
      return entity.UniqueId;
    }

    protected override string Serialize(vCard vcard)
    {
      using (var writer = new StringWriter())
      {
        _vCardImprovedWriter.Write(vcard, writer);
        writer.Flush();
        var newvCardString = writer.GetStringBuilder().ToString();
        return newvCardString;
      }
    }

    protected override bool TryDeserialize(
      string vcardData,
      out vCard vcard,
      WebResourceName uriOfAddressbookForLogging,
      vCardStandardReader deserializer,
      ILoadEntityLogger logger)
    {
      vcard = null;

      // fix some linebreak issues with Open-Xchange
      string normalizedVcardData = vcardData.Contains("\r\r\n") ? ContactDataPreprocessor.NormalizeLineBreaks(vcardData) : vcardData;
      string fixedVcardData = ContactDataPreprocessor.FixRevisionDate(normalizedVcardData);
      string fixedVcardData2 = ContactDataPreprocessor.FixUrlType(fixedVcardData);
      string fixedVcardData3 = ContactDataPreprocessor.FixPhoto(fixedVcardData2);

      try
      {
        vcard = Deserialize(fixedVcardData3, deserializer);
        return true;
      }
      catch (Exception x)
      {
        s_logger.Error(string.Format("Could not deserialize vcardData of '{0}':\r\n{1}", uriOfAddressbookForLogging, fixedVcardData3), x);
        logger.LogSkipLoadBecauseOfError(uriOfAddressbookForLogging, x);
        return false;
      }
    }

    private static vCard Deserialize(string vcardData, vCardStandardReader serializer)
    {
      using (var reader = new StringReader(vcardData))
      {
        return serializer.Read(reader);
      }
    }
  }
}