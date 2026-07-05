---
name: export-excel
description: 'Full Excel export pattern: database query, backend handler, endpoint,
  and frontend download hook. Trigger: When implementing Excel export, data download,
  or export button.'
metadata:
  phase:
  - construction
  layer:
  - database
  - backend
  - frontend
  enforcement: recommended
  depends_on:
  - database-sp
  - data-access
  - react-hooks
  consumed_by:
  - agent-fullstack
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: skill-contract
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Reuse existing list SP/query with `IsExport` flag | ALWAYS | No separate export SP |
| Export block BEFORE pagination block | ALWAYS | Avoid OFFSET/FETCH on export |
| Pass page=1, pageSize=1 from handler when IsExport=true | ALWAYS | DB parameters still required |
| Use string type for dates in backend request | RECOMMENDED | ORM/Dapper date handling |
| Verify error in first result row before generating file | ALWAYS | SP/query may return error |
| Use `[AsParameters]` binding for export endpoint (.NET) | ALWAYS | Query param binding |

## Pattern Overview
```
SP/Query (add IsExport flag) → Backend Handler (ClosedXML/.NET or equiv) →
Export Endpoint → Frontend (hook + useEffect download trigger → button)
```

## Database Layer (SQL Server SP)
```sql
-- Add @ParamIIsExport BIT = 0 to existing list SP
-- Export block goes BEFORE pagination:
---------------------------------------------------------------
-- STEP: EXPORT (before pagination)
---------------------------------------------------------------
IF @ParamIIsExport = 1
BEGIN
    SELECT
        e.EntityId,
        e.Name,
        e.Amount,
        e.RecordCreationDate
    FROM [{Schema}].[{Entity}] e WITH(NOLOCK)
    WHERE e.RecordStatus = 'A'
    -- apply same filters as list
    ORDER BY e.RecordCreationDate DESC;
    RETURN;
END
-- Then the normal paginated block follows
```

## Backend Handler (.NET + ClosedXML)
```csharp
public async Task<ExportResponse> HandleAsync(ExportRequest request, CancellationToken ct)
{
    var list = (await _db.QueryAsync<dynamic>(
        {Module}StoredProcedures.List{Entity},
        new { ParamI... = request..., ParamIIsExport = true, ParamIPage = 1, ParamIPageSize = 1 },
        commandType: CommandType.StoredProcedure)).ToList();

    SpResultHelper.ThrowIfError(list[0]);  // Check for errors

    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add("{Entity}");
    
    // Headers
    sheet.Cell(1, 1).Value = "ID";
    sheet.Cell(1, 2).Value = "Name";
    
    // Data rows
    int row = 2;
    foreach (var item in list)
    {
        var dict = (IDictionary<string, object>)item;
        sheet.Cell(row, 1).Value = dict.GetValue<int>("EntityId");
        sheet.Cell(row, 2).Value = dict.GetValue<string>("Name") ?? string.Empty;
        row++;
    }
    
    using var ms = new MemoryStream();
    workbook.SaveAs(ms);
    return new ExportResponse {
        FileBase64 = Convert.ToBase64String(ms.ToArray()),
        FileName = $"{Entity}_{DateTime.UtcNow:yyyyMMdd}.xlsx",
        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };
}
```

## Python Alternative (openpyxl)
```python
async def export_entities(params: ExportParams, db: AsyncSession):
    rows = await db.execute(text("EXEC Schema.ListEntity @IsExport = 1 ..."))
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.append(["ID", "Name", "Amount"])
    for row in rows:
        ws.append([row.entity_id, row.name, row.amount])
    # Return as base64 or stream
```

## Frontend Hook (React + TanStack Query)
```typescript
export function useExport{Entity}(params: ExportParams, enabled: boolean) {
  const query = useQuery({
    queryKey: ['export-{entities}', params],
    queryFn: () => api.get<ExportResponse>('/entities/export', { params }),
    enabled,
    staleTime: Infinity,
  });

  useEffect(() => {
    if (query.data?.fileBase64) {
      const link = document.createElement('a');
      link.href = `data:${query.data.contentType};base64,${query.data.fileBase64}`;
      link.download = query.data.fileName;
      link.click();
    }
  }, [query.data]);

  return query;
}
```

## Frontend Button
```tsx
const [exportEnabled, setExportEnabled] = useState(false);
const exportQuery = useExport{Entity}(filters, exportEnabled);

// Reset after export completes
useEffect(() => {
  if (exportQuery.isSuccess) setExportEnabled(false);
}, [exportQuery.isSuccess]);

<Button
  onClick={() => setExportEnabled(true)}
  loading={exportQuery.isLoading}
  icon={<DownloadOutlined />}
>
  Export Excel
</Button>
```
