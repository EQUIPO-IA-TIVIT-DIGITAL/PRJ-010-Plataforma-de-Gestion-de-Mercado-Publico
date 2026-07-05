import { test, expect, request } from '@playwright/test';

const API_BASE = process.env.API_BASE || 'http://localhost:5001';
const TEST_USER = { email: 'admin@tivit.cl', password: 'test123' };

async function getAuthToken(): Promise<string> {
  const ctx = await request.newContext({ baseURL: API_BASE });
  const res = await ctx.post('/api/v1/auth/login', {
    data: TEST_USER,
    headers: { 'Content-Type': 'application/json' },
  });
  expect(res.ok()).toBeTruthy();
  const body = await res.json();
  await ctx.dispose();
  return body.data.token;
}

test.describe('Analisis API @regression', () => {

  test('GET /api/v1/analisis/workspaces requires auth @smoke', async ({ request }) => {
    const res = await request.get(`${API_BASE}/api/v1/analisis/workspaces`);
    expect(res.status()).toBe(401);
  });

  test('GET /api/v1/analisis/workspaces returns paginated list @smoke', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const res = await ctx.get('/api/v1/analisis/workspaces?pageSize=5');
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body.success).toBe(true);
    expect(body.data.items).toBeDefined();
    expect(Array.isArray(body.data.items)).toBe(true);
    expect(body.data.page).toBe(1);
    expect(body.data.pageSize).toBe(5);
    expect(body.data.totalRecords).toBeGreaterThanOrEqual(0);
    expect(ctx).toBeDefined();
    await ctx.dispose();
  });

  test('GET /api/v1/analisis/workspaces?search filters by name @regression', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });

    const create = await ctx.post('/api/v1/analisis/workspaces', {
      data: { nombre: `Searchable-${Date.now()}` },
    });
    expect(create.ok()).toBeTruthy();
    const { data: created } = await create.json();

    const search = await ctx.get(`/api/v1/analisis/workspaces?search=Searchable`);
    expect(search.ok()).toBeTruthy();
    const body = await search.json();
    expect(body.data.items.some((w: { id: number }) => w.id === created.id)).toBe(true);

    await ctx.delete(`/api/v1/analisis/workspaces/${created.id}`);
    await ctx.dispose();
  });

  test('POST /api/v1/analisis/workspaces creates a workspace @smoke', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const nombre = `E2E Workspace ${Date.now()}`;
    const res = await ctx.post('/api/v1/analisis/workspaces', {
      data: { nombre },
    });
    expect(res.status()).toBe(201);
    const body = await res.json();
    expect(body.success).toBe(true);
    expect(body.data.nombre).toBe(nombre);
    expect(body.data.estado).toBe('pendiente');
    expect(body.data.id).toBeGreaterThan(0);

    await ctx.delete(`/api/v1/analisis/workspaces/${body.data.id}`);
    await ctx.dispose();
  });

  test('POST /api/v1/analisis/workspaces with empty nombre returns 400 @critical', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const res = await ctx.post('/api/v1/analisis/workspaces', {
      data: { nombre: '' },
    });
    expect(res.status()).toBe(400);
    const body = await res.json();
    expect(body.success).toBe(false);
    await ctx.dispose();
  });

  test('POST /api/v1/analisis/workspaces with whitespace nombre returns 400 @regression', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const res = await ctx.post('/api/v1/analisis/workspaces', {
      data: { nombre: '   ' },
    });
    expect(res.status()).toBe(400);
    await ctx.dispose();
  });

  test('POST with non-existent licitacionId returns 400 @regression', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const res = await ctx.post('/api/v1/analisis/workspaces', {
      data: { nombre: 'Test con licitacion invalida', licitacionId: 999999 },
    });
    expect(res.status()).toBe(400);
    await ctx.dispose();
  });

  test('GET /api/v1/analisis/workspaces/{id} returns detail @smoke', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });

    const create = await ctx.post('/api/v1/analisis/workspaces', {
      data: { nombre: `Detail-${Date.now()}` },
    });
    const { data: created } = await create.json();

    const detail = await ctx.get(`/api/v1/analisis/workspaces/${created.id}`);
    expect(detail.ok()).toBeTruthy();
    const body = await detail.json();
    expect(body.data.id).toBe(created.id);
    expect(body.data.estado).toBe('pendiente');
    expect(body.data.documentosCount).toBe(0);

    await ctx.delete(`/api/v1/analisis/workspaces/${created.id}`);
    await ctx.dispose();
  });

  test('GET /api/v1/analisis/workspaces/{id} non-existent returns 404 @regression', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const res = await ctx.get('/api/v1/analisis/workspaces/99999999');
    expect(res.status()).toBe(404);
    await ctx.dispose();
  });

  test('DELETE /api/v1/analisis/workspaces/{id} soft-deletes @smoke', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });

    const create = await ctx.post('/api/v1/analisis/workspaces', {
      data: { nombre: `ToDelete-${Date.now()}` },
    });
    const { data: created } = await create.json();

    const del = await ctx.delete(`/api/v1/analisis/workspaces/${created.id}`);
    expect(del.ok()).toBeTruthy();

    const after = await ctx.get(`/api/v1/analisis/workspaces/${created.id}`);
    expect(after.status()).toBe(404);
    await ctx.dispose();
  });

  test('DELETE on non-existent workspace returns error @regression', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const res = await ctx.delete('/api/v1/analisis/workspaces/99999999');
    expect([404, 500]).toContain(res.status());
    await ctx.dispose();
  });

  test('GET /api/v1/analisis/workspaces?estado=pendiente filters by state @regression', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const res = await ctx.get('/api/v1/analisis/workspaces?estado=pendiente');
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    for (const item of body.data.items) {
      expect(item.estado).toBe('pendiente');
    }
    await ctx.dispose();
  });

  test('Response envelope has expected shape @critical', async () => {
    const token = await getAuthToken();
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const res = await ctx.get('/api/v1/analisis/workspaces?pageSize=1');
    const body = await res.json();
    expect(body).toHaveProperty('success');
    expect(body).toHaveProperty('data');
    expect(body).toHaveProperty('errors');
    expect(body).toHaveProperty('correlationId');
    await ctx.dispose();
  });
});
