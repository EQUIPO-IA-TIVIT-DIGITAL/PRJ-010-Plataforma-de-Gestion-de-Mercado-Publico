import { Page, Locator } from '@playwright/test';

export class LicitacionesPage {
  readonly page: Page;
  readonly title: Locator;
  readonly table: Locator;
  readonly rows: Locator;
  readonly syncButton: Locator;
  readonly smartSearchButton: Locator;
  readonly searchInput: Locator;
  readonly resetFiltersButton: Locator;
  readonly pagination: Locator;
  readonly montoDesdeInput: Locator;
  readonly estadoSelect: Locator;
  readonly presupuestoHeader: Locator;

  constructor(page: Page) {
    this.page = page;
    this.title = page.getByRole('heading', { name: 'Licitaciones' });
    this.table = page.getByTestId('licitaciones-table');
    this.rows = page.locator('.ant-table-tbody tr');
    this.syncButton = page.locator('button').filter({ hasText: /sincronizar/i });
    this.smartSearchButton = page.locator('button').filter({ hasText: /búsqueda inteligente/i });
    this.searchInput = page.getByTestId('filter-busqueda');
    this.resetFiltersButton = page.getByTestId('filter-reset');
    this.pagination = page.locator('.ant-pagination');
    this.montoDesdeInput = page.getByTestId('filter-monto-desde');
    this.estadoSelect = page.getByTestId('filter-estado');
    this.presupuestoHeader = page.getByRole('columnheader', { name: 'Presupuesto' });
  }

  async goto() {
    await this.page.goto('/licitaciones');
    await this.page.waitForLoadState('networkidle');
    await this.table.waitFor({ state: 'visible', timeout: 15000 });
  }

  async waitForReady() {
    await this.page.waitForLoadState('networkidle');
    await this.table.waitFor({ state: 'visible', timeout: 15000 });
  }

  async waitForTableLoad() {
    await this.page.waitForLoadState('networkidle');
    await this.table.waitFor({ state: 'visible', timeout: 15000 });
    // Wait for at least one row or empty state
    await this.page.waitForTimeout(500);
  }

  async getRowCount(): Promise<number> {
    return await this.rows.count();
  }

  async clickFirstRow() {
    // Esperar una fila de datos real (con data-row-key): el tbody siempre trae
    // una measure-row oculta y clickearla revienta con timeout si los datos aún cargan.
    const firstDataRow = this.page.locator('.ant-table-tbody tr[data-row-key]').first();
    await firstDataRow.waitFor({ state: 'visible', timeout: 15000 });
    await firstDataRow.click();
    await this.page.waitForSelector('.ant-drawer', { timeout: 5000 });
  }

  async filterByMontoDesde(value: number) {
    await this.montoDesdeInput.fill(value.toString());
    // Press Enter to trigger filter
    await this.montoDesdeInput.press('Enter');
    await this.page.waitForLoadState('networkidle');
    await this.page.waitForTimeout(500);
  }

  async filterByEstado(value: string) {
    // Ant Design Select: click to open dropdown
    await this.estadoSelect.click();
    // Wait for dropdown to appear and click option
    // Try multiple selectors for Ant Design dropdown options
    const option = this.page.locator('.ant-select-dropdown:visible .ant-select-item').filter({ hasText: value }).first();
    await option.waitFor({ state: 'visible', timeout: 5000 });
    await option.click();
    await this.page.waitForLoadState('networkidle');
    await this.page.waitForTimeout(500);
  }

  async getAllMontos(): Promise<number[]> {
    const montos: number[] = [];
    const rowCount = await this.rows.count();
    
    if (rowCount === 0) {
      return montos;
    }

    // Presupuesto is typically column index 4 (0-indexed: 5th column)
    for (let i = 0; i < rowCount; i++) {
      const row = this.rows.nth(i);
      const cells = row.locator('td');
      const cellCount = await cells.count();
      
      if (cellCount === 0) continue;
      
      // Try index 4 first (5th column), then 3
      let presupuestoCell = cells.nth(4);
      if (cellCount <= 4) {
        presupuestoCell = cells.nth(3);
      }
      
      try {
        const text = await presupuestoCell.textContent({ timeout: 3000 });
        if (text) {
          const monto = this.parseMonto(text);
          if (!isNaN(monto)) {
            montos.push(monto);
          }
        }
      } catch {
        // Skip rows where we can't read the cell
        continue;
      }
    }
    return montos;
  }

  private parseMonto(text: string): number {
    // Parse Chilean format: $ 50.000.000 or similar
    const cleaned = text.replace(/[^\d.,]/g, '').replace(/\./g, '').replace(',', '.');
    return parseFloat(cleaned);
  }

  async sortByPresupuesto(dir: 'asc' | 'desc') {
    // Use getByRole to avoid strict mode violation (only matches the actual column header)
    await this.presupuestoHeader.click();
    await this.page.waitForLoadState('networkidle');
    await this.page.waitForTimeout(500);
    
    // If we need specific direction, click again for desc
    if (dir === 'desc') {
      // Check if already sorted desc by looking at sort icon
      const sortIcon = this.presupuestoHeader.locator('.ant-table-column-sorter-down, .anticon-down');
      const isDesc = await sortIcon.isVisible().catch(() => false);
      if (!isDesc) {
        await this.presupuestoHeader.click();
        await this.page.waitForLoadState('networkidle');
        await this.page.waitForTimeout(500);
      }
    }
  }

  async resetFilters() {
    await this.resetFiltersButton.click();
    await this.page.waitForLoadState('networkidle');
    await this.page.waitForTimeout(500);
  }

  async getCellText(rowIndex: number, columnIndex: number): Promise<string> {
    const row = this.rows.nth(rowIndex);
    const cells = row.locator('td');
    const cellCount = await cells.count();
    
    if (columnIndex >= cellCount) {
      // Column index out of bounds, try to find by header text in the row
      return '';
    }
    
    const cell = cells.nth(columnIndex);
    try {
      return (await cell.textContent({ timeout: 3000 })) || '';
    } catch {
      return '';
    }
  }

  async getCellTextByHeader(rowIndex: number, headerName: string): Promise<string> {
    const row = this.rows.nth(rowIndex);
    const cells = row.locator('td');
    const cellCount = await cells.count();
    
    if (cellCount === 0) {
      return '';
    }
    
    // Use known working indices based on header name (from getAllMontos which works)
    const indicesMap: Record<string, number[]> = {
      'Presupuesto': [4, 3, 5, 2],
      'Institución': [3, 2, 4, 1],
      'Estado': [5, 4, 6, 3],
    };
    
    const indicesToTry = indicesMap[headerName] || [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
    
    for (const idx of indicesToTry) {
      if (idx >= cellCount) continue;
      
      const cell = cells.nth(idx);
      try {
        const text = await cell.textContent({ timeout: 2000 });
        if (text && text.trim()) {
          return text.trim();
        }
      } catch {
        continue;
      }
    }
    
    return '';
  }

  async getPresupuestoText(rowIndex: number): Promise<string> {
    const row = this.rows.nth(rowIndex);
    const cells = row.locator('td');
    const cellCount = await cells.count();
    
    if (cellCount === 0) return '';
    
    // Try index 4 first (5th column), then 3 - same as getAllMontos
    let idx = 4;
    if (cellCount <= 4) idx = 3;
    if (idx >= cellCount) idx = cellCount - 1;
    
    const cell = cells.nth(idx);
    try {
      // Try innerText first (more reliable for rendered text), then textContent
      const text = await cell.innerText({ timeout: 3000 });
      return text || '';
    } catch {
      try {
        const text = await cell.textContent({ timeout: 3000 });
        return text || '';
      } catch {
        return '';
      }
    }
  }

  async getInstitucionText(rowIndex: number): Promise<string> {
    const row = this.rows.nth(rowIndex);
    const cells = row.locator('td');
    const cellCount = await cells.count();
    
    if (cellCount === 0) return '';
    
    // Try index 3 first (4th column), then 2
    let idx = 3;
    if (cellCount <= 3) idx = 2;
    if (idx >= cellCount) idx = cellCount - 1;
    
    const cell = cells.nth(idx);
    try {
      const text = await cell.innerText({ timeout: 3000 });
      return text || '';
    } catch {
      try {
        const text = await cell.textContent({ timeout: 3000 });
        return text || '';
      } catch {
        return '';
      }
    }
  }

  async getEstadoText(rowIndex: number): Promise<string> {
    const row = this.rows.nth(rowIndex);
    const cells = row.locator('td');
    const cellCount = await cells.count();
    
    if (cellCount === 0) return '';
    
    // Try index 5 first (6th column), then 4
    let idx = 5;
    if (cellCount <= 5) idx = 4;
    if (idx >= cellCount) idx = cellCount - 1;
    
    const cell = cells.nth(idx);
    try {
      const text = await cell.innerText({ timeout: 3000 });
      return text || '';
    } catch {
      try {
        const text = await cell.textContent({ timeout: 3000 });
        return text || '';
      } catch {
        return '';
      }
    }
  }

  async getPresupuestoFormattedText(rowIndex: number): Promise<string> {
    return await this.getPresupuestoText(rowIndex);
  }

  async getMontoDesdeInputValue(): Promise<string> {
    return await this.montoDesdeInput.inputValue();
  }

  async getColumnIndexByHeader(headerName: string): Promise<number> {
    // Find in thead only to avoid measurement row
    const headers = this.page.locator('thead th[aria-label]');
    const count = await headers.count();
    for (let i = 0; i < count; i++) {
      const header = headers.nth(i);
      const label = await header.getAttribute('aria-label');
      if (label?.toLowerCase().includes(headerName.toLowerCase())) {
        return i;
      }
    }
    // Fallback to common indices
    const fallback: Record<string, number> = {
      'Presupuesto': 4,
      'Institución': 3,
      'Estado': 5,
    };
    return fallback[headerName] ?? 0;
  }
}
