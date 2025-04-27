using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

public class Dino : MonoBehaviour
{
    static public int deads = 0;
    static public int counter = 0;
    
    public Rigidbody2D dinoBody;
    public Neurons_Network brain;
    public GameManager gameManager;
    public EnemiesSpawn spawnComponent;

    public int index;
    public int rank;
    public float fitness {get; private set;}

    private bool alive;
    private int groundLevel;
    private float spawnTime;

    void Start()
    {
        dinoBody = GetComponent<Rigidbody2D>();
        gameObject.layer = 6;
        gameManager = GameObject.FindGameObjectWithTag("Manager").GetComponent<GameManager>();
        spawnComponent = GameObject.FindGameObjectWithTag("Enemies").GetComponent<EnemiesSpawn>();

        alive = true;
        index = counter;
        groundLevel = -4;
        spawnTime = Time.time;

        if(GameManager.generation > 1) brain = new Neurons_Network(3, 10, 1, 1, GameManager.newGenerationWeights[index]);
        else brain = new Neurons_Network(3, 10, 1, 1);

        Debug.Log(brain);

        counter++;

        /*if(GameManager.generation > 1)
        {
            for(int i = 0; i < GameManager.newGenerationBrain.Count; i++)
            {
                brain = GameManager.newGenerationBrain[i];
            }
        }*/
    }

    void Update()
    {
        if(spawnComponent.enemies[0].transform.position.x <= -8) spawnComponent.enemies.RemoveAt(0);

        if(alive)
        {
           fitness = Time.time;
           Think();
        }

        /*if(Input.GetKeyDown(KeyCode.Space) && transform.position.y < groundLevel)
        {
            Jump();
        }

        if(Input.GetKeyDown(KeyCode.W) && transform.position.y < groundLevel)
        {
            Jump();
        }*/
    }

    void Think()
    {
        GameObject enemy = Enemy();

        if(enemy != null)
        {
            double[] inputs = new double[3];

            inputs[0] = enemy.transform.position.x;
            inputs[1] = enemy.GetComponent<RectTransform>().sizeDelta.x;
            inputs[2] = enemy.GetComponent<RectTransform>().sizeDelta.y;

            double[] output = brain.StartWorking(inputs);

            if(output[0] > 0.5 && transform.position.y < groundLevel) Jump();
        }
    }

    GameObject Enemy()
    {
        int i = 0;

        while(i < spawnComponent.enemies.Count)
        {
            if(transform.position.x > spawnComponent.enemies[i].transform.position.x) i++;
            else return spawnComponent.enemies[i];
        }
        
        return null;
    }

    void Jump()
    {
        dinoBody.velocity = new Vector2(0, 7);
    }

    void OnTriggerEnter2D(Collider2D enemy)
    {
        if(enemy.tag == "Respawn") 
        {
            deads++;
            this.alive = false;

            this.rank = deads;
            
            GameManager.neuronNetworks[this.rank - 1] = this.brain;
            
            //gameManager.dinosBrain.Add(brain);

            Destroy(gameObject);
        }

        if(deads == 10)
        {
            deads = 0;
            counter = 0;

            gameManager.GeneticSelection();

            SceneManager.LoadScene(0);
        }
    }
}