import OpenAI from 'openai';
import dotenv from 'dotenv';

dotenv.config();

const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY
});

const MODEL = process.env.OPENAI_MODEL || 'gpt-4o-mini';

export async function analizarDocumento(texto, prompt) {
  try {
    const response = await openai.chat.completions.create({
      model: MODEL,
      messages: [
        { role: 'system', content: prompt },
        { role: 'user', content: `Analiza este documento:\n\n${texto}` }
      ],
      temperature: 0.3,
      max_tokens: 4000,
      response_format: { type: 'json_object' }
    });

    const content = response.choices[0].message.content;
    return JSON.parse(content);
  } catch (error) {
    console.error('Error en análisis IA:', error.message);
    throw error;
  }
}

export async function analizarConContexto(documentos, prompt) {
  const textosCombinados = documentos.map(d =>
    `=== ${d.nombre} ===\n${d.texto}\n`
  ).join('\n\n');

  return await analizarDocumento(textosCombinados, prompt);
}
