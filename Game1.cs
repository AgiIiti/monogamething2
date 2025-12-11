using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Text.Json;
using System.Diagnostics;

namespace yur;

public class RectangleData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
public class RectanglesFile
{
    public List<RectangleData> Rectangles { get; set; }
}

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    Texture2D picture;

    Texture2D backgroundTexture;
    Texture2D backgroundTexture2;
    Background background;
    Background background2;

    Texture2D patrollerTexture;
    // Lista för alla fiender
    private List<Enemy> enemies = new();

    private List<IGameObject> gameObjects = new();

    private List<Rectangle> platforms = new();
    private Texture2D pixel;
    private float gravity = 1200f;
    public int groundy;

    Player player = new();

    private float worldwidth = 3000f;
    private Vector2 cameraPosition;

    public int playerdeathcount = 0;
    private SpriteFont font;

    private bool allowDoubleJump = false;
    private bool SpaceLastFrame = false;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1000;
        _graphics.PreferredBackBufferHeight = 700;
        _graphics.ApplyChanges();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        var viewport = GraphicsDevice.Viewport;
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.Azure });
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        picture = Content.Load<Texture2D>("mushroom sprite (2)");

        //bakgrundsgrejs
        backgroundTexture = Content.Load<Texture2D>("background1");
        backgroundTexture2 = Content.Load<Texture2D>("trees");
        background = new Background(backgroundTexture, 0.1f);
        background2 = new Background(backgroundTexture2, 0.3f);

        player.Position = new Vector2(
            100f,
            viewport.Height / 2f - picture.Height / 2f
        );

        groundy = viewport.Height - picture.Height + 5;

        string json = System.IO.File.ReadAllText("data.json");
        RectanglesFile data = JsonSerializer.Deserialize<RectanglesFile>(json);

        if (data != null)
        {
            for (int i = 0; i < data.Rectangles.Count; i++)
            {
                RectangleData rect = data.Rectangles[i];
                platforms.Add(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height));
            }
        }

        platforms.Add(new Rectangle(
                -5000,
                groundy,
                (int)worldwidth * 3,
                100
            ));

        cameraPosition = Vector2.Zero;

        patrollerTexture = Content.Load<Texture2D>("patroller");
        enemies.Add(EnemyFactory.CreateChaser(new Vector2(600, 400), patrollerTexture, player));
        enemies.Add(EnemyFactory.CreatePatroller(new Vector2(1000, 400), patrollerTexture, 900, 1100));
        font = Content.Load<SpriteFont>("MyFont");


        gameObjects.Add(new PowerUp(new Vector2(800, 400), PowerUpType.JumpBoost, 30, 10f));
        gameObjects.Add(new Teleporter(new Vector2(600, 400), new Vector2(1500, 300), 50, 50));
        gameObjects.Add(new PowerUp(new Vector2(400, 300), PowerUpType.DoubleJump));



    }

    protected override void Update(GameTime gameTime)
    {
        
        var viewport = GraphicsDevice.Viewport;
        var keyboard = Keyboard.GetState();
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        var playerposition = player.Position;
        var playervelocity = player.Velocity;

        var previousPosition = playerposition;

        if (keyboard.IsKeyDown(Keys.A))
        {
            playerposition.X -= player.Speed * dt;
        }
        if (keyboard.IsKeyDown(Keys.D))
        {
            playerposition.X += player.Speed * dt;
        }

        if (player.IsOnGround)
            allowDoubleJump = true;

        if (keyboard.IsKeyDown(Keys.Space) && SpaceLastFrame == false)
        {
            if (player.IsOnGround)
            {
                playervelocity.Y = player.JumpSpeed;
                player.IsOnGround = false;
            }
            else if (player.HasDoubleJump && allowDoubleJump)
            {
                playervelocity.Y = player.JumpSpeed;
                allowDoubleJump = false;
            }
        }

        SpaceLastFrame = keyboard.IsKeyDown(Keys.Space);

        float targetCamX = playerposition.X - viewport.Width / 2f;

        player.IsOnGround = false;
        
        playervelocity.Y += gravity * dt;
        playerposition += playervelocity * dt;

        Rectangle playerHitbox = new Rectangle(
            (int)playerposition.X,
            (int)playerposition.Y,
            picture.Width,
            picture.Height
        );


        foreach (var platform in platforms)
        {
            float feetNow = playerposition.Y + picture.Height;
            float feetPrev = previousPosition.Y + picture.Height;

            if (playervelocity.Y >= 0)
            {
                // Did we cross the platform top this frame?
                if (feetPrev <= platform.Top && feetNow >= platform.Top &&
                    playerposition.X + picture.Width > platform.Left &&
                    playerposition.X < platform.Right)
                {
                    playerposition.Y = platform.Top - picture.Height;
                    playervelocity.Y = 0;
                    player.IsOnGround = true;
                }
            }
        }

        player.Position = playerposition;
        player.Velocity = playervelocity;

        // spelarens hitbox
        playerHitbox = new Rectangle(
            (int)player.Position.X,
            (int)player.Position.Y,
            picture.Width,
            picture.Height
        );


        foreach (var obj in gameObjects)
        {
            obj.Update(dt, playerHitbox, ref player);
        }


        if (player.Position.Y >= groundy)
        {
            player.Position = new Vector2(player.Position.X, groundy);
            player.Velocity = new Vector2(player.Velocity.X, 0);
            player.IsOnGround = true;
        }



        player.Position = new Vector2(
            MathHelper.Clamp(player.Position.X, -worldwidth - picture.Width, worldwidth - picture.Width),
            player.Position.Y
        );

        cameraPosition.X = targetCamX;

        // Kör genom alla fiender i listan
        foreach (var enemy in enemies)
        {
            enemy.Update(dt, platforms, gravity, player);
        }

        // Tittar efter en collision och isf lägger till
        foreach (var enemy in enemies)
        {
            if (playerHitbox.Intersects(enemy.Hitbox))
            {
                playerdeathcount += 1;
            }
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _spriteBatch.Begin();

        var playerScreenPosition = new Vector2(
            player.Position.X - cameraPosition.X,
            player.Position.Y - cameraPosition.Y
        );

        Vector2 backgroundPosition = new Vector2(-cameraPosition.X * background.Speed - 200, 0);
        Vector2 backgroundPosition_2 = new Vector2(-cameraPosition.X * background.Speed - 400, 0);
        Vector2 backgroundPosition2 = new Vector2(-cameraPosition.X * background2.Speed - 200, -400);

        _spriteBatch.Draw(backgroundTexture, backgroundPosition, null, Color.White, 0f, new Vector2(0, 0), 2f, SpriteEffects.None, 0f);
        _spriteBatch.Draw(backgroundTexture, backgroundPosition_2, null, Color.White, 0f, new Vector2(0, 0), 2f, SpriteEffects.None, 0f);
        _spriteBatch.Draw(backgroundTexture2, backgroundPosition2, null, Color.White, 0f, new Vector2(0, 0), 3f, SpriteEffects.None, 0f);
        _spriteBatch.Draw(picture, playerScreenPosition, null, Color.White, 0f, new Vector2(0, 0), 1f, SpriteEffects.None, 0f);

        foreach (var platform in platforms)
        {
            var screenRect = new Rectangle(
                (int)(platform.X - cameraPosition.X),
                platform.Y,
                platform.Width,
                platform.Height
            );
            _spriteBatch.Draw(pixel, screenRect, Color.Azure);
        }

        // gå genom alla fiender och rita 
        foreach (var enemy in enemies)
        {
            enemy.Draw(_spriteBatch, cameraPosition);
        }
        _spriteBatch.DrawString(font, "Player Deaths: " + playerdeathcount.ToString(), new Vector2(100, 80), Color.White);

        foreach (var obj in gameObjects)
        {

            obj.Draw(_spriteBatch, cameraPosition, pixel);
        }


        _spriteBatch.End();

        base.Draw(gameTime);
    }
}