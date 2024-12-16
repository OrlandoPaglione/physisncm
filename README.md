# **PhysisNCM**

PhysisNCM é uma API para classificação de produtos utilizando a Nomenclatura Comum do Mercosul (NCM). A API permite consultar o código NCM com base em descrições de produtos, utilizando inteligência artificial para análise e, opcionalmente, validação dos códigos por meio de uma tabela NCM local.

---

## **Funcionalidades**

- **Consulta NCM via OpenAI API**:
  - Busca o código NCM mais adequado para a descrição do produto usando inteligência artificial.
- **Validação com Tabela NCM**:
  - Verifica a validade do código NCM retornado com base em uma tabela local.
- **Fallback com Descrição Expandida**:
  - Caso a descrição seja insuficiente, tenta expandi-la e buscar novamente.
- **Cache de Consultas**:
  - Implementa cache em memória para evitar consultas repetidas à API.

---

## **Tecnologias Utilizadas**

- **.NET 6 / ASP.NET Core**: Framework principal para a API.
- **MemoryCache**: Para caching em memória.
- **OpenAI API**: Para inteligência artificial.
- **FuzzySharp** (opcional): Para busca baseada em similaridade.

---

## **Requisitos**

- **SDK do .NET 6 ou superior**.
- Conta na **OpenAI** com chave de API válida.
- Tabela NCM em formato JSON (opcional para validação).

---

## **Instalação**

1. Clone este repositório:
   ```bash
   git clone https://github.com/seu-usuario/PhysisNCM.git
   cd PhysisNCM
   ```

2. Restaure as dependências do projeto:
   ```bash
   dotnet restore
   ```

3. Configure sua chave de API no arquivo `appsettings.json` ou diretamente no código:
   ```json
   {
       "OpenAI": {
           "ApiKey": "sua-chave-de-api"
       }
   }
   ```

4. Adicione sua tabela NCM no diretório `dados` (opcional):
   - Exemplo de tabela:
     ```json
     {
         "Nomenclaturas": [
             {
                 "Codigo": "8517.12.31",
                 "Descricao": "Celulares e aparelhos para comunicação sem fio",
                 "Data_Inicio": "01/01/2022",
                 "Data_Fim": "31/12/9999"
             }
         ]
     }
     ```

5. Execute o projeto:
   ```bash
   dotnet run --urls "http://localhost:8080"
   ```

---

## **Uso**

### **Endpoints Disponíveis**

#### **POST /api/ncm/consulta**

Consulta o código NCM baseado na descrição de um produto.

- **Exemplo de Request:**
  ```json
  {
      "descricao": "Celular LG K12 Plus 32GB Android 8.1"
  }
  ```

- **Exemplo de Resposta:**
  ```json
  {
      "descricao": "Celular LG K12 Plus 32GB Android 8.1",
      "ncm": "O código NCM mais adequado para o Celular LG K12 Plus 32GB com Android 8.1 é 8517.12.31.",
      "ncm_codigo": "8517.12.31",
      "aproximado": "Não, a classificação é precisa."
  }
  ```

#### **Tabela NCM**
Se a tabela local estiver carregada, o sistema tenta validar o código NCM retornado ou sugere um código alternativo com base na descrição.

---

## **Configuração do Cache**

- A API utiliza o **MemoryCache** para armazenar os resultados por padrão por **10 minutos**.
- O tempo de cache pode ser configurado no controlador (`TimeSpan.FromMinutes(10)`).

---

## **Estrutura do Projeto**

```plaintext
PhysisNCM/
├── Controllers/
│   └── NcmController.cs    # Controlador principal da API
├── Models/
│   ├── ProdutoRequest.cs   # Modelo para requisições de produtos
│   └── NcmEntry.cs         # Modelo para a tabela NCM
├── Services/
│   └── NcmTableLoader.cs   # Serviço para carregar e validar a tabela NCM
├── appsettings.json        # Configurações da API
├── Program.cs              # Configuração e inicialização do projeto
├── dados/
│   └── Tabela_NCM.json     # Tabela NCM em JSON (opcional)
```

---

## **Próximos Passos**

1. **Cache Distribuído:**
   - Substituir o `MemoryCache` por Redis para ambientes distribuídos.

2. **Interface Gráfica:**
   - Criar uma interface web para consultas manuais.

3. **Melhoria de Busca:**
   - Implementar correspondência mais robusta usando aprendizado de máquina ou algoritmos de similaridade.

4. **Integração com Receita Federal:**
   - Adicionar consulta direta à Receita Federal para validar códigos NCM.

---

## **Contribuição**

Sinta-se à vontade para abrir issues ou pull requests para melhorar o projeto!

---

## **Licença**

Este projeto está licenciado sob a **MIT License**. Consulte o arquivo `LICENSE` para mais detalhes.
```

