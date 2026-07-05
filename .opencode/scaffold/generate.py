#!/usr/bin/env python3
"""
Scaffolding Generator for Framework Agéntico.

Parses an api-first-spec markdown document and generates
backend, frontend, database, and test scaffolding.

Usage:
    python3 generate.py <spec-file> [--output <output-dir>] [--namespace <ns>] [--schema <schema>]
"""

import re
import os
import sys
import argparse
from string import Template
from pathlib import Path


# ─── Template loading ────────────────────────────────────────────────────────

TEMPLATES_DIR = Path(__file__).parent / "templates"


def load_template(name):
    path = TEMPLATES_DIR / name
    if not path.exists():
        raise FileNotFoundError(f"Template not found: {path}")
    return Template(path.read_text())


# ─── Pluralization ───────────────────────────────────────────────────────────

def pluralize(word):
    if word.endswith(("s", "x", "z", "ch", "sh")):
        return word + "es"
    if word.endswith("y") and len(word) > 2 and word[-2] not in "aeiou":
        return word[:-1] + "ies"
    return word + "s"


# ─── Spec parsing ────────────────────────────────────────────────────────────

def parse_spec(filepath):
    with open(filepath, "r") as f:
        content = f.read()

    module_name = extract_module_name(content)
    entities = extract_entities(content)
    endpoints = extract_endpoints(content)
    dtos = extract_dtos(content)

    return {
        "module": module_name,
        "entities": entities,
        "endpoints": endpoints,
        "dtos": dtos,
    }


def extract_module_name(content):
    match = re.search(r"^#\s+(?:Module[:\s]+)?(.+)$", content, re.MULTILINE)
    if not match:
        print("Warning: No module title found, using 'UnknownModule'")
        return "UnknownModule"
    name = match.group(1).strip()
    name = "".join(word.capitalize() for word in re.split(r"[\s_/-]+", name))
    return name


def extract_entities(content):
    entities = []
    section = re.search(
        r"##\s*(?:Entity|ERD|Database|Tables|Model).*?\n(.+?)(?=\n##\s|\Z)",
        content,
        re.DOTALL | re.IGNORECASE,
    )
    if not section:
        return entities

    body = section.group(1)
    table_pattern = re.compile(
        r"^\|(.+)\|\s*$", re.MULTILINE
    )

    lines = body.split("\n")
    current_entity = None
    fields = []
    header_mode = True

    for i, line in enumerate(lines):
        ent_match = re.match(r"^###\s+(.+)$", line)
        if ent_match:
            if current_entity and fields:
                entities.append({"name": current_entity, "fields": fields})
            current_entity = ent_match.group(1).strip()
            fields = []
            header_mode = True
            continue

        if not current_entity:
            continue

        if not line.strip().startswith("|"):
            if not header_mode:
                if current_entity and fields:
                    entities.append({"name": current_entity, "fields": fields})
                current_entity = None
                fields = []
                header_mode = True
            continue

        parts = [p.strip() for p in line.strip().strip("|").split("|")]
        if len(parts) < 2:
            continue

        if header_mode:
            if all(p.strip("-: ") == "" for p in parts):
                header_mode = False
            continue

        field_name = parts[0].strip("`* ")
        field_type = parts[1].strip("`* ") if len(parts) > 1 else "string"
        field_desc = parts[2].strip() if len(parts) > 2 else ""
        fields.append({"name": field_name, "type": field_type, "desc": field_desc})

    if current_entity and fields:
        entities.append({"name": current_entity, "fields": fields})

    if not entities:
        first_table = extract_first_table(body)
        if first_table:
            entities.append({"name": "Entity", "fields": first_table})

    return entities


def extract_first_table(body):
    lines = body.strip().split("\n")
    in_table = False
    fields = []
    for line in lines:
        if line.strip().startswith("|") and line.strip().endswith("|"):
            parts = [p.strip() for p in line.strip().strip("|").split("|")]
            parts = [p for p in parts if p]
            if len(parts) >= 2:
                if not in_table:
                    in_table = True
                    continue
                if all(p.strip("-: ") == "" for p in parts):
                    continue
                field_name = parts[0].strip("`* ")
                field_type = parts[1].strip("`* ")
                fields.append({"name": field_name, "type": field_type, "desc": parts[2].strip() if len(parts) > 2 else ""})
        elif in_table and fields:
            break
    return fields if fields else None


def extract_endpoints(content):
    section = re.search(
        r"##\s*(?:Endpoint|API|Routes).*?\n(.+?)(?=\n##\s|\Z)",
        content,
        re.DOTALL | re.IGNORECASE,
    )
    if not section:
        return []

    body = section.group(1)
    endpoints = []
    lines = body.strip().split("\n")
    in_header = True

    for line in lines:
        if not line.strip().startswith("|"):
            in_header = False
            continue
        parts = [p.strip() for p in line.strip().strip("|").split("|")]
        parts = [p for p in parts if p]
        if len(parts) < 2:
            continue
        if in_header:
            if all(p.strip("-: ") == "" for p in parts):
                in_header = False
            continue
        method = parts[0].strip().upper()
        path = parts[1].strip()
        desc = parts[2].strip() if len(parts) > 2 else ""
        endpoints.append({"method": method, "path": path, "desc": desc})

    return endpoints


def extract_dtos(content):
    dtos = {}
    section = re.search(
        r"##\s*(?:DTO|Request|Response|Types).*?\n(.+?)(?=\n##\s|\Z)",
        content,
        re.DOTALL | re.IGNORECASE,
    )
    if not section:
        return dtos

    body = section.group(1)
    lines = body.split("\n")
    current_dto = None
    fields = []
    header_mode = True

    for line in lines:
        dto_match = re.match(r"^###\s+(.+)$", line)
        if dto_match:
            if current_dto and fields:
                dtos[current_dto] = fields
            current_dto = dto_match.group(1).strip()
            fields = []
            header_mode = True
            continue

        if not current_dto:
            continue

        if not line.strip().startswith("|"):
            if not header_mode:
                if current_dto and fields:
                    dtos[current_dto] = fields
                current_dto = None
                fields = []
                header_mode = True
            continue

        parts = [p.strip() for p in line.strip().strip("|").split("|")]
        parts = [p for p in parts if p]
        if len(parts) < 2:
            continue

        if header_mode:
            if all(p.strip("-: ") == "" for p in parts):
                header_mode = False
            continue

        field_name = parts[0].strip("`* ")
        field_type = parts[1].strip("`* ")
        field_required = parts[2].strip() if len(parts) > 2 else "Yes"
        fields.append({"name": field_name, "type": field_type, "required": field_required})

    if current_dto and fields:
        dtos[current_dto] = fields

    return dtos


# ─── Name helpers ────────────────────────────────────────────────────────────

def camel(s):
    return s[0].lower() + s[1:] if s else s


def clean_field_type(t):
    t = t.strip("`")
    mapping = {
        "int": "int",
        "integer": "int",
        "string": "string",
        "str": "string",
        "bool": "bool",
        "boolean": "bool",
        "datetime": "DateTime",
        "date": "DateTime",
        "decimal": "decimal",
        "float": "decimal",
        "double": "decimal",
        "guid": "Guid",
        "enum": "string",
        "text": "string",
    }
    nullable = t.endswith("?")
    base = t.rstrip("?")
    cs_type = mapping.get(base.lower(), "string")
    if nullable and cs_type != "string":
        cs_type += "?"
    return cs_type


def cs_field_decl(field):
    t = clean_field_type(field["type"])
    return f"    public {t} {field['name']} {{ get; set; }}"


def cs_field_init(field):
    t = clean_field_type(field["type"])
    if t == "string":
        return f"            {field['name']} = string.Empty,"
    if t in ("int", "decimal", "double", "float"):
        return f"            {field['name']} = 0,"
    return f"            {field['name']} = default,",


def ts_type(t):
    t = t.strip("`").rstrip("?").lower()
    mapping = {
        "int": "number",
        "integer": "number",
        "string": "string",
        "str": "string",
        "bool": "boolean",
        "boolean": "boolean",
        "datetime": "string",
        "date": "string",
        "decimal": "number",
        "float": "number",
        "double": "number",
        "guid": "string",
        "enum": "string",
        "text": "string",
    }
    result = mapping.get(t, "string")
    if t.strip("`").rstrip("?").lower() in ("int", "integer", "decimal", "float", "double"):
        result = "number"
    nullable = t.strip("`").endswith("?")
    if nullable or t.strip("`").lower() in ("datetime", "date", "guid"):
        result += " | null"
    return result


def sql_type(t):
    t = t.strip("`").rstrip("?").lower()
    mapping = {
        "int": "INT",
        "integer": "INT",
        "string": "NVARCHAR(500)",
        "str": "NVARCHAR(500)",
        "text": "NVARCHAR(MAX)",
        "bool": "BIT",
        "boolean": "BIT",
        "datetime": "DATETIME2",
        "date": "DATE",
        "decimal": "DECIMAL(18,2)",
        "float": "DECIMAL(18,6)",
        "double": "DECIMAL(18,6)",
        "guid": "UNIQUEIDENTIFIER",
        "enum": "NVARCHAR(50)",
    }
    return mapping.get(t, "NVARCHAR(500)")


# ─── Substitution context ────────────────────────────────────────────────────

def build_context(spec, entity):
    entities = pluralize(entity["name"])
    entity_name = entity["name"]
    module = spec["module"]
    module_camel = camel(module)
    entity_camel = camel(entity_name)

    ctx = {
        "MODULE": module,
        "MODULE_CAMEL": module_camel,
        "ENTITY": entity_name,
        "ENTITIES": entities[0].upper() + entities[1:] if entities else entities,
        "entity": entity_camel,
        "entities": entities,
        "SCHEMA": spec.get("schema", "dbo"),
        "NAMESPACE": spec.get("namespace", f"Tivit.{module}"),

        "CS_FIELDS": "\n".join(cs_field_decl(f) for f in entity["fields"]),
        "TS_FIELDS": "\n".join(
            f"  {f['name'][0].lower() + f['name'][1:]}: {ts_type(f['type'])};"
            for f in entity["fields"]
        ),
        "SQL_COLUMNS": ",\n    ".join(
            f"[{f['name']}] {sql_type(f['type'])}"
            + (" NOT NULL" if not f["type"].endswith("?") else " NULL")
            for f in entity["fields"]
        ),
        "ENTITY_FIELDS_CSV": ", ".join(f["name"] for f in entity["fields"]),
        "SP_PARAMS": ",\n    ".join(
            f"    @ParamI{f['name']} {sql_type(f['type'])}"
            + (" = NULL" if f["type"].endswith("?") else "")
            for f in entity["fields"]
        ),
    }
    return ctx


# ─── File generation ─────────────────────────────────────────────────────────

def write_file(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)
    print(f"  Created: {path}")


def generate(spec, output_dir):
    output = Path(output_dir)
    output.mkdir(parents=True, exist_ok=True)

    for entity in spec["entities"]:
        ctx = build_context(spec, entity)
        entity_name = entity["name"]
        entities = ctx["entities"]
        entity_camel = ctx["entity"]
        module = ctx["MODULE"]
        module_lower = ctx["MODULE_CAMEL"]

        # Backend files
        backend_dir = output / "backend"
        write_file(backend_dir / f"{entity_name}Module.cs", f"""using Microsoft.AspNetCore.Http.HttpResults;
using {ctx["NAMESPACE"]}.Shared;

namespace {ctx["NAMESPACE"]}.Modules.{entity_name};

public static class {entity_name}Module
{{
    public static void Map(IEndpointRouteBuilder app)
    {{
        var group = app.MapGroup("/api/{entities}")
            .WithTags("{entity_name}")
            .RequireAuthorization();

        {entity_name}Endpoints.Map(group);
    }}
}}
""")
        write_file(backend_dir / f"{entity_name}Endpoints.cs", load_template("endpoint.cs.j2").safe_substitute(ctx))
        write_file(backend_dir / f"{entity_name}Handler.cs", load_template("handler.cs.j2").safe_substitute(ctx))
        write_file(backend_dir / f"{entity_name}Request.cs", f"""namespace {ctx["NAMESPACE"]}.Modules.{entity_name};

public class List{entity_name}Request
{{
    public int Page {{ get; set; }} = 1;
    public int PageSize {{ get; set; }} = 20;
    public string? SortBy {{ get; set; }}
    public string? SortOrder {{ get; set; }}
    public string? SearchFilter {{ get; set; }}
}}

public class Create{entity_name}Request
{{
{chr(10).join(cs_field_decl(f) for f in entity["fields"] if f["name"].lower() != "id")}
}}

public class Update{entity_name}Request
{{
{chr(10).join(cs_field_decl(f) for f in entity["fields"] if f["name"].lower() != "id")}
}}
""")
        write_file(backend_dir / f"{entity_name}Response.cs", f"""namespace {ctx["NAMESPACE"]}.Modules.{entity_name};

public class {entity_name}ListItem
{{
{chr(10).join(cs_field_decl(f) for f in entity["fields"])}
}}

public class {entity_name}DetailResponse
{{
{chr(10).join(cs_field_decl(f) for f in entity["fields"])}
}}

public class Create{entity_name}Response
{{
{chr(10).join(cs_field_decl(f) for f in entity["fields"])}
}}

public class Update{entity_name}Response
{{
{chr(10).join(cs_field_decl(f) for f in entity["fields"])}
}}
""")

        # Frontend files
        frontend_dir = output / "frontend"
        write_file(frontend_dir / "types.ts", load_template("types.ts.j2").safe_substitute(ctx))
        write_file(frontend_dir / f"use{entity_name}List.ts", load_template("hook.ts.j2").safe_substitute(ctx))
        write_file(frontend_dir / f"use{entity_name}Mutation.ts", f"""import {{ useMutation, useQueryClient }} from '@tanstack/react-query';
import api from '@/shared/lib/api';
import type {{ Create{entity_name}Request, Create{entity_name}Response, Update{entity_name}Request, Update{entity_name}Response }} from './types';

export function useCreate{entity_name}() {{
  const queryClient = useQueryClient();

  return useMutation<Create{entity_name}Response, Error, Create{entity_name}Request>({{
    mutationFn: (data) => api.post<Create{entity_name}Response>('/{entities}', data),
    onSuccess: () => {{
      queryClient.invalidateQueries({{ queryKey: ['{entities}'] }});
    }},
  }});
}}

export function useUpdate{entity_name}() {{
  const queryClient = useQueryClient();

  return useMutation<Update{entity_name}Response, Error, {{ id: number; data: Update{entity_name}Request }}>({{
    mutationFn: ({{ id, data }}) => api.put<Update{entity_name}Response>(`/{entities}/${{id}}`, data),
    onSuccess: () => {{
      queryClient.invalidateQueries({{ queryKey: ['{entities}'] }});
    }},
  }});
}}

export function useDelete{entity_name}() {{
  const queryClient = useQueryClient();

  return useMutation<void, Error, number>({{
    mutationFn: (id) => api.delete(`/{entities}/${{id}}`),
    onSuccess: () => {{
      queryClient.invalidateQueries({{ queryKey: ['{entities}'] }});
    }},
  }});
}}
""")
        write_file(frontend_dir / f"{entity_name}List.tsx", load_template("component.tsx.j2").safe_substitute(ctx))
        write_file(frontend_dir / f"{entity_name}Form.tsx", f"""import {{ Form, Input, Select, Button, Card }} from 'antd';
import type {{ Create{entity_name}Request, Update{entity_name}Request }} from './types';

interface {entity_name}FormProps {{
  initialValues?: Update{entity_name}Request;
  onSubmit: (values: Create{entity_name}Request | Update{entity_name}Request) => void;
  isPending: boolean;
}}

export default function {entity_name}Form({{ initialValues, onSubmit, isPending }}: {entity_name}FormProps) {{
  const [form] = Form.useForm();
  const isEdit = !!initialValues;

  return (
    <Card title={{isEdit ? `Edit {entity_name}` : `Create {entity_name}`}}>
      <Form
        form={{form}}
        layout="vertical"
        initialValues={{initialValues}}
        onFinish={{onSubmit}}
      >
{chr(10).join(f'''        <Form.Item name="{f['name'][0].lower() + f['name'][1:]}" label="{f['name']}" rules={{{{[{{ required: false }}]}}}}>
          <Input />
        </Form.Item>''' for f in entity["fields"] if f["name"].lower() != "id")}
        <Form.Item>
          <Button type="primary" htmlType="submit" loading={{isPending}}>
            {{isEdit ? 'Update' : 'Create'}}
          </Button>
        </Form.Item>
      </Form>
    </Card>
  );
}}
""")
        write_file(frontend_dir / f"{entity_name}Page.tsx", f"""import {{ useState }} from 'react';
import {{ Button, Modal }} from 'antd';
import {{ use{entity_name}List }} from './use{entity_name}List';
import {{ useCreate{entity_name}, useUpdate{entity_name}, useDelete{entity_name} }} from './use{entity_name}Mutation';
import {entity_name}List from './{entity_name}List';
import {entity_name}Form from './{entity_name}Form';
import type {{ {entity_name}ListItem }} from './types';

export default function {entity_name}Page() {{
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<{entity_name}ListItem | null>(null);
  const [showForm, setShowForm] = useState(false);

  const {{ data, isLoading, error }} = use{entity_name}List({{ page, pageSize: 20 }});
  const {{ mutate: create, isPending: creating }} = useCreate{entity_name}();
  const {{ mutate: update, isPending: updating }} = useUpdate{entity_name}();
  const {{ mutate: remove }} = useDelete{entity_name}();

  const handleCreate = (values: any) => {{
    create(values, {{ onSuccess: () => setShowForm(false) }});
  }};

  const handleUpdate = (values: any) => {{
    if (!editing) return;
    update({{ id: editing.id, data: values }}, {{ onSuccess: () => {{ setEditing(null); setShowForm(false); }} }});
  }};

  const handleDelete = (id: number) => {{
    Modal.confirm({{
      title: 'Delete {entity_name}',
      content: 'Are you sure?',
      onOk: () => remove(id),
    }});
  }};

  return (
    <div>
      <div style={{{{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}}}>
        <h2>{entity_name}s</h2>
        <Button type="primary" onClick={{() => setShowForm(true)}}>New {entity_name}</Button>
      </div>

      <{entity_name}List
        data={{data?.items ?? []}}
        isLoading={{isLoading}}
        error={{error}}
        pagination={{{{ page, pageSize: 20, totalRecords: data?.total ?? 0 }}}}
        onPageChange={{(p) => setPage(p)}}
        onEdit={{(id) => {{
          const item = data?.items?.find((i: {entity_name}ListItem) => i.id === id);
          if (item) {{ setEditing(item); setShowForm(true); }}
        }}}}
        onDelete={{handleDelete}}
      />

      <Modal
        title={{editing ? 'Edit {entity_name}' : 'Create {entity_name}'}}
        open={{showForm}}
        onCancel={{() => {{ setShowForm(false); setEditing(null); }}}}
        footer={{null}}
      >
        <{entity_name}Form
          initialValues={{editing ?? undefined}}
          onSubmit={{editing ? handleUpdate : handleCreate}}
          isPending={{creating || updating}}
        />
      </Modal>
    </div>
  );
}}
""")
        write_file(frontend_dir / "index.ts", f"""export {{ default as {entity_name}Page }} from './{entity_name}Page';
export {{ default as {entity_name}List }} from './{entity_name}List';
export {{ default as {entity_name}Form }} from './{entity_name}Form';
export * from './types';
""")

        # Database files
        db_dir = output / "database"
        write_file(db_dir / f"001_create_{entities}.sql", load_template("sql_create.sql.j2").safe_substitute(ctx))
        write_file(db_dir / f"002_sp_{entities}_crud.sql", load_template("sql_sp.sql.j2").safe_substitute(ctx))

        # Test files
        tests_dir = output / "tests"
        write_file(tests_dir / f"{module_lower}.spec.ts", load_template("test.spec.ts.j2").safe_substitute(ctx))


# ─── CLI entry point ─────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="Generate project scaffolding from api-first-spec")
    parser.add_argument("spec", help="Path to the spec markdown file")
    parser.add_argument("--output", "-o", default="./output", help="Output directory")
    parser.add_argument("--namespace", "-n", default=None, help=".NET namespace override")
    parser.add_argument("--schema", "-s", default="dbo", help="Database schema name")
    args = parser.parse_args()

    if not os.path.exists(args.spec):
        print(f"Error: Spec file not found: {args.spec}")
        sys.exit(1)

    print(f"Parsing spec: {args.spec}")
    spec = parse_spec(args.spec)
    spec["namespace"] = args.namespace or f"Tivit.{spec['module']}"
    spec["schema"] = args.schema

    print(f"Module: {spec['module']}")
    print(f"Entities: {[e['name'] for e in spec['entities']]}")
    print(f"Endpoints: {len(spec['endpoints'])}")
    print(f"\nGenerating scaffolding in: {args.output}")

    generate(spec, args.output)

    print(f"\nDone. Output: {args.output}")
    print(f"  Backend: {args.output}/backend/")
    print(f"  Frontend: {args.output}/frontend/")
    print(f"  Database: {args.output}/database/")
    print(f"  Tests: {args.output}/tests/")


if __name__ == "__main__":
    main()
